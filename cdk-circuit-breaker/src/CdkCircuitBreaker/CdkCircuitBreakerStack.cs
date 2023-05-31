using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;

namespace CdkCircuitBreaker
{
    public class CdkCircuitBreakerStack : Stack
    {
        internal CdkCircuitBreakerStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            #region iamroles
            var iamLambdaRole = new Role(this,"LambdaExecutionRole", new RoleProps
            {
                RoleName = "LambdaExecutionRole",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
            });
            
            iamLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AmazonDynamoDBFullAccess"));
            iamLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"));
            iamLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayFullAccess"));
            iamLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSStepFunctionsFullAccess"));

            var iamStepFunctionRole = new Role(this,"step_functions_basic_execution", new RoleProps
            {
                RoleName = "step_functions_basic_execution",
                AssumedBy = new ServicePrincipal("states.amazonaws.com")
            });
            
            iamStepFunctionRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("CloudWatchLogsFullAccess"));
            iamStepFunctionRole.AddManagedPolicy(ManagedPolicy.FromManagedPolicyArn(this,"AWSLambdaRole","arn:aws:iam::aws:policy/service-role/AWSLambdaRole"));
            iamStepFunctionRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayFullAccess"));
            #endregion iamroles
            
            #region DynamoDB tables
            
            var circuitBreakerTable = new Table(this, "CircuitBreaker", new TableProps
            {
                TableName = "CircuitBreaker",
                PartitionKey = new Attribute
                {
                    Name = "ServiceName",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                  Name  = "ExpireTimeStamp",
                  Type = AttributeType.NUMBER
                },
                TimeToLiveAttribute = "ExpireTimeStamp",
                RemovalPolicy = RemovalPolicy.DESTROY,
                ReadCapacity = 5,
                WriteCapacity = 5
            });
            
            #endregion
            
            #region Lambda Functions

            var getCircuitStatusLambda = new Function(this,"GetCircuitStatus", new FunctionProps
            {
                FunctionName = "GetCircuitStatus",
                Runtime = Runtime.DOTNET_6,
                Handler = "GetCircuitStatusLambda::GetCircuitStatusLambda.GetCircuitStatus::FunctionHandler",
                Role = iamLambdaRole,
                Code = Code.FromAsset("lambdas/GetCircuitStatusLambda.zip"),
                Timeout = Duration.Seconds(300)
            });
            
            var updateCircuitStatusLambda = new Function(this,"UpdateCircuitStatus", new FunctionProps
            {
                FunctionName = "UpdateCircuitStatus",
                Runtime = Runtime.DOTNET_6,
                Handler = "UpdateCircuitStatusLambda::UpdateCircuitStatusLambda.UpdateCircuitStatus::FunctionHandler",
                Role = iamLambdaRole,
                Code = Code.FromAsset("lambdas/UpdateCircuitStatusLambda.zip"),
                Timeout = Duration.Seconds(300)
            });
            
            #endregion
            
            #region stepfunction
             
            var circuitClosed = new Succeed(this,"Circuit Closed");
            var circuitOpen = new Fail(this, "Circuit Open");
            
            var getCircuitStatusTask = new LambdaInvoke(this, "Get Circuit Status", new LambdaInvokeProps
            {
                LambdaFunction = getCircuitStatusLambda,
                Comment = "Get Circuit Status",
                RetryOnServiceExceptions = false,
                PayloadResponseOnly = true
            });
            
            var updateCircuitStatusTask = new LambdaInvoke(this, "Update Circuit Status", new LambdaInvokeProps
            {
                LambdaFunction = updateCircuitStatusLambda,
                Comment = "Update Circuit Status",
                RetryOnServiceExceptions = false,
                PayloadResponseOnly = true
            }).Next(circuitOpen);

            var stateJson = new Dictionary<string, object>
            {
                {"Type", "Task"},
                {"Next", "Circuit Closed"},
                {"Resource", "arn:aws:states:::lambda:invoke"},
                {
                    "Parameters", new Dictionary<string, object>
                    {
                        {"FunctionName.$", "$.TargetLambda"}
                    }
                },
                {
                    "Comment",
                    "Task to execute lambda. This will set circuit status to OPEN if the execution fails for three times or the task times out"
                },
                {"TimeoutSeconds", 12},
                {
                    "Retry", new []
                    {
                        new Dictionary<string,object>()
                        {
                            {"BackoffRate", 1.5},
                            {"MaxAttempts", 3},
                            {"IntervalSeconds", 2},
                            {"ErrorEquals", new string[] {Errors.TASKS_FAILED, Errors.TIMEOUT}}
                        }
                    }
                },
                {
                    "Catch", new []
                    {
                        new Dictionary<string, object>()
                        {
                            {"ErrorEquals", new string[] {Errors.TASKS_FAILED, Errors.TIMEOUT}},
                            {"Next", "Update Circuit Status"},
                            {"ResultPath", "$.taskresult"}
                        }
                    }
                }
            };
            
            var executeLambdaTask = new CustomState(this, "Execute Lambda", new CustomStateProps
            {
                StateJson = stateJson
                
            });

            var stepDefinition = Chain.Start(getCircuitStatusTask)
                .Next(new Choice(this, "Is Circuit Closed")
                    .When(Condition.StringEquals("$.CircuitStatus", "OPEN"), circuitOpen)
                    .When(Condition.StringEquals("$.CircuitStatus", ""), executeLambdaTask.Next(circuitClosed)));
         
            var stateMachine = new StateMachine(this, "CircuitBreaker-StepFunction", new StateMachineProps {
                StateMachineName = "CircuitBreaker-StepFunction",
                StateMachineType = StateMachineType.STANDARD,
                Role = iamStepFunctionRole,
                TracingEnabled = true,
                Definition = stepDefinition
            });
            
            #endregion
        }
    }
}

