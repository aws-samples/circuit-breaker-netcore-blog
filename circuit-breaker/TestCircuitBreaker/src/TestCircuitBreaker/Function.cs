using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestCircuitBreaker
{
    public class Function
    {
        public FunctionData FunctionHandler(FunctionData fnData, ILambdaContext context)
        {
            Thread.Sleep(15000);
            return fnData;
        }
    }
    
    public class FunctionData
    {
        public string TargetLambda { get; set; }
        public string CircuitStatus { get; set; }
        public string JsonPayload { get; set; }
    }
}