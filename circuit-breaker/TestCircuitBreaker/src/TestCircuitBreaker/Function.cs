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
        public FunctionData FunctionHandler(FunctionData functionData, ILambdaContext context)
        {
            if (functionData.PassTest == 0)
                Thread.Sleep(15000);
            return functionData;
        }
    }
    
       public class FunctionData
    {
        public long PassTest { get; set; }
        public TaskResultFields taskresult { get; set; }
        public string JsonPayload { get; set; }

    }
    public class TaskResultFields
    {
        public string Error { get; set; }
        public string Cause { get; set; }
    }        
}