using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using GetCircuitStatusLambda;

namespace GetCircuitStatusLambda.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void TestFunctionHandler()
        {
            // Invoke the lambda function and confirm the string was upper cased.
            var function = new GetCircuitStatus();
            var context = new TestLambdaContext();
            var functionData = new FunctionData();
            functionData.TargetLambda = "HelloWorld";
            var result = function.FunctionHandler(functionData, context);
            
            Assert.Equal("", result.CircuitStatus);

        }
    }
}