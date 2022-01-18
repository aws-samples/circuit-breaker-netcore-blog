using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetCircuitStatusLambda
{
    public class GetCircuitStatus
    {
        private static AmazonDynamoDBClient client = new AmazonDynamoDBClient();
        private DynamoDBContext _dbContext = new DynamoDBContext(client);
        public FunctionData FunctionHandler(FunctionData functionData, ILambdaContext context)
        {
            string serviceName = functionData.TargetLambda;
            var serviceDetails = _dbContext.LoadAsync<CircuitBreaker>(serviceName);

            if (serviceDetails.Result != null)
            {
                functionData.CircuitStatus = serviceDetails.Result.CircuitStatus;
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
        
        [DynamoDBProperty]
        public long ExpireTimeStamp { get; set; }
    }


    public class FunctionData
    {
        public string TargetLambda { get; set; }
        public string CircuitStatus { get; set; }
    }
}