using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using UpdateCircuitStatusLambda;

namespace UpdateCircuitStatusLambda.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void TestToUpperFunction()
        {
            // Invoke the lambda function and confirm the string was upper cased.
            var function = new UpdateCircuitStatus();
            var context = new TestLambdaContext();
            var fnData = new FunctionData();
            fnData.TargetLambda = "TestCircuitBreaker";
            var result = function.FunctionHandler(fnData, context);

            Assert.Equal("OPEN", result.CircuitStatus);
        }
    }
}