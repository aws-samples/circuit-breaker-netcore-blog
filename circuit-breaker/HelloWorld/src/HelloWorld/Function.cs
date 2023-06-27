using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        public FunctionData FunctionHandler(FunctionData fnData, ILambdaContext context)
        {
            context.Logger.Log("Hello World from successful circuit");

            if (fnData.taskresult is null)
                fnData.taskresult = new TaskResultFields();

            fnData.taskresult.Error = "";
            return fnData;
        }
    }
    
   public class FunctionData
    {
        public TaskResultFields taskresult { get; set; }
        public string JsonPayload { get; set; }

    }
    public class TaskResultFields
    {
        public string Error { get; set; }
        public string Cause { get; set; }
    }        
}