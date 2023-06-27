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
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UpdateCircuitStatusLambda
{
    public class UpdateCircuitStatus
    {
        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private static string tableName = "CircuitBreaker";

        public async Task<FunctionData> FunctionHandler(FunctionData functionData, ILambdaContext context)
        {

            // If GetCircuitStatus encounters an error accessing the CircuitBreaker Table,
            //  there won't be a record to update.
            if ((functionData.CircuitControl is null) ||
                (functionData.CircuitControl.HiResTimeStamp == 0))
            {
                context.Logger.Log("CircuitControl.HiResTimeStamp is zero. Unable to update CallResult");
                return functionData;
            }

            string serviceName = functionData.TargetLambda;

            int callResult;
            if (functionData.taskresult is null || functionData.taskresult.Error == "")
                callResult = (int)ResultTypes.Success;
            else
                callResult = (int)ResultTypes.Fail;

            // Define item key
            //  Hash-key of the target item is string value functionData.TargetLambda (a.k.a. serviceName)
            //  Range-key of the target item is string value functionData.HiResTimeStamp
            Dictionary<string, AttributeValue> key = new Dictionary<string, AttributeValue>
            {
                { "ServiceName", new AttributeValue { S = serviceName } },
                { "HiResTimeStamp", new AttributeValue { N = functionData.CircuitControl.HiResTimeStamp.ToString() } }
            };

            // Define attribute updates
            Dictionary<string, AttributeValueUpdate> updates = new Dictionary<string, AttributeValueUpdate>();
            // Update item's Setting attribute
            updates["CallResult"] = new AttributeValueUpdate()
            {
                Action = AttributeAction.PUT,
                Value = new AttributeValue { N = callResult.ToString() }
            };

            // Create UpdateItem request
            UpdateItemRequest request = new UpdateItemRequest
            {
                TableName = tableName,
                Key = key,
                AttributeUpdates = updates
            };

            // Issue request
            await client.UpdateItemAsync(request);

            return functionData;
        }
    }
    
    
    [DynamoDBTable("CircuitBreaker")]
       public class CircuitBreaker
    {
        [DynamoDBHashKey]
        public string ServiceName { get; set; }

       [DynamoDBProperty]
        public string CircuitStatus { get; set; }
        public ResultTypes CallResult { get; set; }
        public long ExpireTimeStamp { get; set; }

        [DynamoDBRangeKey]
        public long HiResTimeStamp { get; set; }
    }
    [Flags]
    public enum ResultTypes
    {
        None      = 0,
        Success = 1,
        Fail = 2
    }

   public class FunctionData
    {
        public string TargetLambda { get; set; }
        public CircuitControl_Fields CircuitControl { get; set; }
        public TaskResultFields taskresult { get; set; }
        public string JsonPayload { get; set; }
    }
        public class CircuitControl_Fields
    {
        public string CircuitStatus { get; set; }
        public long HiResTimeStamp { get; set; }
        public int Count { get; set; }
        public int MaxAttempts { get; set; }
        public int MaxConsecutive { get; set; }
        public int MaxHalfRate { get; set; }
        public int InactivityResetSeconds { get; set; }
        public int HalfIntervalSeconds { get; set; }
        public int TimeoutSeconds { get; set; }        
   }
public class TaskResultFields
    {
        public string Error { get; set; }
        public string Cause { get; set; }
    }            
}