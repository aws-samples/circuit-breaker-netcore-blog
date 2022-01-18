using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using HelloWorld;

namespace HelloWorld.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void TestHelloWorld()
        {
            var function = new Function();
            var context = new TestLambdaContext();
            var fnData = new FunctionData();
            
            var retValue = function.FunctionHandler(fnData, context);
        }
    }
}