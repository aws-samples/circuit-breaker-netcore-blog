using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UpdateCircuitStatusLambda
{
    public class UpdateCircuitStatus
    {
        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private DynamoDBContext _dbContext = new DynamoDBContext(client, new DynamoDBContextConfig {ConsistentRead = true});

        public async Task<FunctionData> FunctionHandler(FunctionData functionData, ILambdaContext context)
        {
            string serviceName = functionData.TargetLambda;
            functionData.CircuitStatus = "OPEN";
            var currentTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            //Example of using scan when TTL attribute is not a sort key 
            // var scan1 = new ScanCondition("ServiceName",ScanOperator.Equal,serviceName);
            // var scan2 = new ScanCondition("ExpireTimeStamp",ScanOperator.GreaterThan,currentTimeStamp);
            // var serviceDetails = _dbContext.ScanAsync<CircuitBreaker>(new []{scan1, scan2}).GetRemainingAsync();
            
            //Example of using query when TTL attribute is a sort key
            var serviceDetails = _dbContext.QueryAsync<CircuitBreaker>(serviceName, QueryOperator.GreaterThan,
                new List<object>
                    {currentTimeStamp}).GetRemainingAsync();
            
            context.Logger.Log(serviceDetails.Result.Count.ToString());
            if (serviceDetails.Result.Count == 0)
            {
                context.Logger.Log("Inside save construct");
                try
                {
                    // increase 20 seconds to a higher value as needed during test
                    var circuitBreaker = new CircuitBreaker
                    {
                        ServiceName = serviceName,
                        CircuitStatus = "OPEN",
                        ExpireTimeStamp = DateTimeOffset.Now.AddSeconds(20).ToUnixTimeSeconds()
                    };
                    await _dbContext.SaveAsync(circuitBreaker);
                }
                catch (Exception ex)
                {
                    context.Logger.Log(ex.Message);
                }
            }
            
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
        
        [DynamoDBRangeKey]
        public long ExpireTimeStamp { get; set; }
    }


    public class FunctionData
    {
        public string TargetLambda { get; set; }
        public string CircuitStatus { get; set; }
    }
}