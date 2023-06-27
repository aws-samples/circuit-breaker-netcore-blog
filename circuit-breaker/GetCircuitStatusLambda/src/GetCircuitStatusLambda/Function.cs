/**************************************************************************************************
Legal Disclaimer: The sample code; software libraries; command line tools; proofs of concept;
templates; or other related technology (including any of the foregoing that are provided by our
personnel) is provided to you as AWS Content under the AWS Customer Agreement, or the relevant
written agreement between you and AWS (whichever applies). You should not use this AWS Content in
your production accounts, or on production or other critical data. You are responsible for testing,
securing, and optimizing the AWS Content, such as sample code, as appropriate for production grade
use based on your specific quality control practices and standards. Deploying AWS Content may incur
AWS charges for creating or using AWS chargeable resources, such as running Amazon EC2 instances or
using Amazon S3 storage.
**************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.CloudWatch.EMF.Config;
using Amazon.CloudWatch.EMF.Environment;
using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetCircuitStatusLambda
{
    public class GetCircuitStatus
    {
        private const int debugLevel = 0;

        // ConsistentRead provides strongly consistent reads at the expense of higher latency and
        //  increased capacity units. As the impact of inconsistency could only result in extra
        //  traffic when throttling, the benefit is not viewed as worth the cost.
        private const Boolean CONSISTENT_READ = false;
        
        // QUERY_PAGE_SIZE of 70 was selected for economy and efficiency as 70 records can be retrieved
        //   for 0.5 LCU.  With 70 concurrent executions, there is the potential to not look back far
        //   enough to find consecutive completed calls.  This is a design tradeoff against cost and
        //   latency because retrieving another page of records would further delay execution.
        private const int QUERY_PAGE_SIZE = 70;
        private const string CIRCUIT_STATUS_CLOSED = "CLOSED";
        private const string CIRCUIT_STATUS_HALF = "HALF";
        private const string CIRCUIT_STATUS_OPEN = "OPEN";

        // Default limit for attempts to loop through the Step Function to execute the Lambda function.
        //  Note that a CircuitStatus of "OPEN" returned by this function overrides MaxAttempts by terminating the Step Function.
        private const int DEF_MAX_ATTEMPTS = 3;

        // Default number of consecutive Successes or Failures before transitioning CircuitStatus to CLOSED or HALF respectively.
        private const int DEF_MAX_CONSECUTIVE = 3;

        // Default number of execution attempts allowed per HalfIntervalSeconds
        private const int DEF_MAX_HALF_RATE = 1;

        // Default number of seconds in the throttling interval with CircuitStatus in HALF (Open/Closed).
        //  Using default values as an example, 1 attempt is allowed if there have been no other attempts
        //  in the last 30 seconds.  The value of 1 comes from DEF_MAX_HALF_RATE.
        //  Note that records with a Success (1) CallResult are excluded from this query.
        //    CallResult values of None (0) meaning in-progress and Fail (2) are counted.
        private const int DEF_HALF_INTERVAL_SECONDS = 30;
        
        // Default number of seconds to retain CircuitBreaker DDB Table records.
        //   With no records, CircuitStatus defaults to CLOSED.
        //   InactivityResetSeconds is programmatically forced to be equal or greater than HalfIntervalSeconds.
        private const int DEF_INACTIVITY_RESET_SECONDS = 300;

        // Default number of seconds for Step Function State "Execute Lambda" to wait
        //  for TargetLambda function to complete.
        private const int DEF_TIMEOUT_SECONDS = 12;

        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private DynamoDBContext _dbContext = new DynamoDBContext(client,new DynamoDBContextConfig {ConsistentRead = CONSISTENT_READ});
        
        public async Task<FunctionData> FunctionHandler(FunctionData functionData, ILambdaContext context)
        {
            string serviceName = functionData.TargetLambda;

            if (functionData.CircuitControl is null)
                functionData.CircuitControl = new CircuitControl_Fields();

            functionData.CircuitControl.CircuitStatus = CIRCUIT_STATUS_CLOSED;

            functionData.CircuitControl.Count++;

            if (functionData.CircuitControl.MaxAttempts <= 0)
                functionData.CircuitControl.MaxAttempts = DEF_MAX_ATTEMPTS;

            if (functionData.CircuitControl.MaxConsecutive <= 0)
                functionData.CircuitControl.MaxConsecutive = DEF_MAX_CONSECUTIVE;

            if (functionData.CircuitControl.MaxHalfRate <= 0)
                functionData.CircuitControl.MaxHalfRate = DEF_MAX_HALF_RATE;

            if (functionData.CircuitControl.HalfIntervalSeconds <= 0)
                functionData.CircuitControl.HalfIntervalSeconds = DEF_HALF_INTERVAL_SECONDS;

            if (functionData.CircuitControl.InactivityResetSeconds <= 0)
                functionData.CircuitControl.InactivityResetSeconds = DEF_INACTIVITY_RESET_SECONDS;

            // Records must not expire before HalfIntervalSeconds
            if (functionData.CircuitControl.HalfIntervalSeconds > functionData.CircuitControl.InactivityResetSeconds)
                functionData.CircuitControl.InactivityResetSeconds = functionData.CircuitControl.HalfIntervalSeconds;

            if (functionData.CircuitControl.TimeoutSeconds <= 0)
                functionData.CircuitControl.TimeoutSeconds = DEF_TIMEOUT_SECONDS;

            functionData.CircuitControl.HiResTimeStamp = DateTimeOffset.Now.AddSeconds(functionData.CircuitControl.InactivityResetSeconds).ToUnixTimeMilliseconds();

        //
        // Step 1.  Determine Status from existing records for this service
        //

            // Attempt to get status from newest (last) record with start time >= Now
            // Records are sorted by the Query
            var currentTimeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Example of using scan when TTL attribute is not a sort key 
            // var scan1 = new ScanCondition("ServiceName",ScanOperator.Equal,serviceName);
            // var scan2 = new ScanCondition("HiResTimeStamp",ScanOperator.GreaterThan,currentTimeStamp);
            // var serviceDetails = _dbContext.ScanAsync<CircuitBreaker>(new []{scan1, scan2}).GetRemainingAsync();
            
            DynamoDBOperationConfig opConfig = new DynamoDBOperationConfig();
            opConfig.BackwardQuery = true;
/*
            //Example of using query when TTL attribute is a sort key to retrieve all records
            var serviceDetails = _dbContext.QueryAsync<CircuitBreaker>(serviceName, QueryOperator.GreaterThan,
                new List<object>
                    {currentTimeStamp}, opConfig).GetRemainingAsync();
*/
            // queryOperationConfig used with FromQUeryAsync allows use of Limit to limit number of table records
            // Retrieve QUERY_PAGE_SIZE records where HiResTimeStamp is greater than currentTimeStamp
            //   The multipler is to account for in-progress calls which are not used for status change detection.
            // Only the newest record is used in Step 1; however other records are used in Step 2.
            var queryOperationConfig = new QueryOperationConfig();
            queryOperationConfig.ConsistentRead = CONSISTENT_READ;
            var keyExpression = new Expression();
            keyExpression.ExpressionStatement = "ServiceName=:pk and HiResTimeStamp>:sk";
            keyExpression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>();
            keyExpression.ExpressionAttributeValues.Add(":pk", serviceName);
            keyExpression.ExpressionAttributeValues.Add(":sk", currentTimeStamp);
            queryOperationConfig.KeyExpression = keyExpression;
            queryOperationConfig.Limit = QUERY_PAGE_SIZE;
            queryOperationConfig.BackwardSearch = true;

            try
            {
                var serviceDetails = _dbContext.FromQueryAsync<CircuitBreaker>(queryOperationConfig, opConfig).GetNextSetAsync();

                if (debugLevel <= 1)
                    context.Logger.Log(serviceName);

                if (debugLevel == 0)
                {
                    context.Logger.Log("currentTimeStamp: " + currentTimeStamp);
                    context.Logger.Log("current HiResTimeStamp: " + functionData.CircuitControl.HiResTimeStamp);

                    context.Logger.Log("Call Records read in Step 1: " + serviceDetails.Result.Count.ToString());
                }

                if (serviceDetails.Result.Count > 0)
                    functionData.CircuitControl.CircuitStatus = serviceDetails.Result[0].CircuitStatus;

                if (debugLevel <= 1)
                    context.Logger.Log("CircuitStatus from CircuitBreaker table: " + functionData.CircuitControl.CircuitStatus);

                //
                // Step 2.  Check for successive failures or successes to transition Circuit Status
                //

                int counter = 0;
                int failCounter = 0;
                int successCounter = 0;

                foreach (var circuitBreaker in serviceDetails.Result)
                {
                    switch(circuitBreaker.CallResult)
                    {
                        case ResultTypes.Fail:
                            ++counter;
                            ++failCounter;
                            if (debugLevel == 0)
                            {                        
                                context.Logger.Log("HiResTimeStamp: " + circuitBreaker.HiResTimeStamp.ToString());
                                context.Logger.Log("CallResult: Fail");
                            }
                            break;
                        case ResultTypes.Success:
                            ++counter;
                            ++successCounter;
                            if (debugLevel == 0)
                            {                        
                                context.Logger.Log("HiResTimeStamp: " + circuitBreaker.HiResTimeStamp.ToString());
                                context.Logger.Log("CallResult: Success");
                            }
                            break;
                        default:
                            // do nothing
                            break;
                    }

                    if (counter >= functionData.CircuitControl.MaxConsecutive)
                    {
                        if (failCounter >= functionData.CircuitControl.MaxConsecutive)
                        {
                            if (functionData.CircuitControl.CircuitStatus == CIRCUIT_STATUS_CLOSED)
                                functionData.CircuitControl.CircuitStatus = CIRCUIT_STATUS_HALF;
                        }
                        else if (successCounter >= functionData.CircuitControl.MaxConsecutive)
                        {
                            if (functionData.CircuitControl.CircuitStatus == CIRCUIT_STATUS_HALF)
                                functionData.CircuitControl.CircuitStatus = CIRCUIT_STATUS_CLOSED;
                        }
                        //Check only the last functionData.CircuitControl.MaxConsecutive completed calls.
                        break;
                    }

                }
            }
            catch (Exception ex)
            {   // Error accessing Table;  TargetLambda call will be allowed
                context.Logger.Log("Error Reading DDB Table for Steps 1,2: " + ex.Message);
                functionData.CircuitControl.HiResTimeStamp = 0;  // Indicator that there is no DDB Table record to update
                return functionData;
            }

            //
            // Step 3.  For Half-Open/Closed case, check for number of records in the interval
            // Note that Records are written with an offset equal to INACTIVITY RESET_SECONDS so
            //   to get records for the interval, time must be adjusted accordingly.
            //
            if (functionData.CircuitControl.CircuitStatus == CIRCUIT_STATUS_HALF)
            {
                if (debugLevel <= 1)
                    context.Logger.Log("Half-open mode.  Checking for throttling...");

                // Query will retrieve records that do not have a CallResult of Success for requests starting 
                //   within the last HALF_INTERVAL_SECONDS period of time.
                var intervalTimeStamp = DateTimeOffset.Now.AddSeconds(functionData.CircuitControl.InactivityResetSeconds
                   -functionData.CircuitControl.HalfIntervalSeconds).ToUnixTimeMilliseconds();

                // Example of using scan when TTL attribute is not a sort key 
                // var scan1 = new ScanCondition("ServiceName",ScanOperator.Equal,serviceName);
                // var scan2 = new ScanCondition("HiResTimeStamp",ScanOperator.GreaterThan,currentTimeStamp);
                // var serviceDetails = _dbContext.ScanAsync<CircuitBreaker>(new []{scan1, scan2}).GetRemainingAsync();

                ScanCondition myFilter = new ScanCondition("CallResult", ScanOperator.NotEqual, ResultTypes.Success);
                List<ScanCondition> scanList = new List<ScanCondition>();
                scanList.Add(myFilter);
                opConfig.QueryFilter = scanList;

                try
                {
                    //Example of using query when TTL attribute is a sort key
                    var serviceDetails = _dbContext.QueryAsync<CircuitBreaker>(serviceName, QueryOperator.GreaterThan,
                        new List<object>
                            {intervalTimeStamp}, opConfig).GetRemainingAsync();

                    if (debugLevel == 0)
                    {
                        context.Logger.Log("Non-Success Records in last " + functionData.CircuitControl.HalfIntervalSeconds 
                        + " seconds : " + serviceDetails.Result.Count.ToString());

                        foreach (var circuitBreaker in serviceDetails.Result)
                        {
                            context.Logger.Log(circuitBreaker.ServiceName);
                            context.Logger.Log(circuitBreaker.HiResTimeStamp.ToString());
                        }
                    }

                    if (serviceDetails.Result.Count >= functionData.CircuitControl.MaxHalfRate)
                        functionData.CircuitControl.CircuitStatus = CIRCUIT_STATUS_OPEN;
                }
                catch (Exception ex)
                {
                    context.Logger.Log("Error Reading DDB Table in Step 3: " + ex.Message);
                    functionData.CircuitControl.HiResTimeStamp = 0;  // Indicator that there is no DDB Table record to update
                    return functionData;   // Error accessing Table;  TargetLambda call will be allowed
                }
            }

            //
            // Step 4.  Write new start record
            //

            if (functionData.CircuitControl.CircuitStatus != CIRCUIT_STATUS_OPEN)
            {   // Write new start record
                try
                {
                    var circuitBreaker = new CircuitBreaker
                    {
                        ServiceName = serviceName,
                        CircuitStatus = functionData.CircuitControl.CircuitStatus,
                        HiResTimeStamp = functionData.CircuitControl.HiResTimeStamp,

                        // DDB TTL must be in seconds; adding 1 second to account for truncation of sub-second time
                        ExpireTimeStamp = (functionData.CircuitControl.HiResTimeStamp/1000) + 1
                    };

                    await _dbContext.SaveAsync(circuitBreaker);
                }
                catch (Exception ex)
                {
                    context.Logger.Log("Error Writing New DDB Record: " + ex.Message);
                    functionData.CircuitControl.HiResTimeStamp = 0;  // Indicator that there is no DDB Table record to update
                }
            }

            if (debugLevel <= 1)
                context.Logger.Log("CircuitStatus returned by function: " + functionData.CircuitControl.CircuitStatus);

            // Provide CloudWatch Metric with CircuitStatus as Dimension
            var envProvider = new EnvironmentProvider(EnvironmentConfigurationProvider.Config, new ResourceFetcher());
            var logger = new MetricsLogger();
            var dimensionSet = new DimensionSet();

            dimensionSet.AddDimension("CircuitStatus", functionData.CircuitControl.CircuitStatus);
            logger.SetDimensions(dimensionSet);
            logger.SetNamespace("CircuitBreaker");
            logger.PutMetric("CircuitBreakerUsage", 1, Unit.COUNT);
            logger.Flush();

            // Provide CloudWatch Metric with CircuitStatus and TargetLambda as Dimensions
            // This metric includes ServiceName and ServiceType dimensions in order
            //    to distinguist an instance of GetCircuitStatus in case of multiple regions.
            //    Those dimensions are included by default with PutDimensions()
            // Provide full Arn as Property and use only the Lambda function name as a Dimension
            logger.PutProperty("TargetLambdaArn", serviceName);            
            int position = serviceName.IndexOf(":function:");
            if (position > 0)
                serviceName = serviceName.Substring(position + 10);
            dimensionSet.AddDimension("TargetLambda", serviceName);
            logger.ResetDimensions(true);
            logger.PutDimensions(dimensionSet);
            logger.PutMetric("CircuitBreakerUsage", 1, Unit.COUNT);
            logger.Flush();

            return functionData;
        }
    }

    [DynamoDBTable("CircuitBreaker")]
    public class CircuitBreaker
    {
        [DynamoDBHashKey]
        public string ServiceName { get; set; }

       [DynamoDBProperty]
        public long ExpireTimeStamp { get; set; }
        public string CircuitStatus { get; set; }
        public ResultTypes CallResult { get; set; }

        [DynamoDBRangeKey]
        public long HiResTimeStamp { get; set; }
    }
    [Flags]
    public enum ResultTypes
    {
        None = 0,
        Success = 1,
        Fail = 2,
    }

    public class FunctionData
    {
        public string TargetLambda { get; set; }
        public CircuitControl_Fields CircuitControl { get; set; }
    }
    public class CircuitControl_Fields
    {
        public string CircuitStatus { get; set; }
        public long HiResTimeStamp { get; set; }
        public int Count { get; set; }
        public int MaxAttempts { get; set; }
        public int MaxConsecutive { get; set; }
        public int MaxHalfRate { get; set; }
        public int HalfIntervalSeconds { get; set; }
        public int InactivityResetSeconds { get; set; }
        public int TimeoutSeconds { get; set; }        
    }
}