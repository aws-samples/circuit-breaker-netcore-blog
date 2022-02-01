using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetCircuitStatusLambda
{
    public class GetCircuitStatus
    {
        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private DynamoDBContext _dbContext = new DynamoDBContext(client,new DynamoDBContextConfig {ConsistentRead = true});
        public FunctionData FunctionHandler(FunctionData functionData, ILambdaContext context)
        {
            string serviceName = functionData.TargetLambda;
            var currentTimeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            // Example of using scan when TTL attribute is not a sort key 
            // var scan1 = new ScanCondition("ServiceName",ScanOperator.Equal,serviceName);
            // var scan2 = new ScanCondition("ExpireTimeStamp",ScanOperator.GreaterThan,currentTimeStamp);
            // var serviceDetails = _dbContext.ScanAsync<CircuitBreaker>(new []{scan1, scan2}).GetRemainingAsync();
            
            //Example of using query when TTL attribute is a sort key
            var serviceDetails = _dbContext.QueryAsync<CircuitBreaker>(serviceName, QueryOperator.GreaterThan,
                new List<object>
                    {currentTimeStamp}).GetRemainingAsync();

            foreach (var circuitBreaker in serviceDetails.Result)
            {
                context.Logger.Log(circuitBreaker.ServiceName);
                context.Logger.Log(circuitBreaker.ExpireTimeStamp.ToString());
            }

            if (serviceDetails.Result.Count > 0)
            {
                functionData.CircuitStatus = serviceDetails.Result[0].CircuitStatus;
            }
            else
            {
                functionData.CircuitStatus = "";
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