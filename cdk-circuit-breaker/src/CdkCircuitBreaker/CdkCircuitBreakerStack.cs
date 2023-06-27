/**************************************************************************************************
Legal Disclaimer: The sample code; software libraries; command line tools; proofs of concept;
templates; or other related technology (including any of the foregoing that are provided by our
personnel) is provided to you as AWS Content under the AWS Customer Agreement, or the relevant
written agreement between you and AWS (whichever applies). You should not use this AWS Content in
your production accounts, or on production or other critical data. You are responsible for testing,
securing, and optimizing the AWS Content, such as sample code, as appropriate for production grade
use based on your specific quality control practices and standards. Deploying AWS Content may incur
AWS charges for creating or using AWS chargeable resources, such as running Amazon EC2 instances or
using Amazon S3 storage.
**************************************************************************************************/
#define INCLUDE_TEST_FUNCTIONS

using Constructs;                   // for Construct class
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Amazon.CDK.AWS.Logs;

namespace CdkCircuitBreaker
{
    public class CdkCircuitBreakerStack : Stack
    {
        internal CdkCircuitBreakerStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
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
                  Name  = "HiResTimeStamp",
                  Type = AttributeType.NUMBER
                },
                TimeToLiveAttribute = "ExpireTimeStamp",
                RemovalPolicy = RemovalPolicy.DESTROY,
                ReadCapacity = 5,
                WriteCapacity = 5
            });
            
            #endregion
            
            #region iamroles
            var iamGetCircuitStatusLambdaRole = new Role(this,"GetCircuitStatusLambdaExecutionRole", new RoleProps
            {
                RoleName = "GetCircuitStatusLambdaExecutionRole",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
            });
            
            circuitBreakerTable.GrantReadWriteData(iamGetCircuitStatusLambdaRole);
            circuitBreakerTable.Grant(iamGetCircuitStatusLambdaRole, "dynamodb:DescribeTable");
            iamGetCircuitStatusLambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                Resources = new [] { "*" },
                Actions = new [] {
                    "logs:CreateLogGroup",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents"
                },
                Effect = Effect.ALLOW
            }));
            iamGetCircuitStatusLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayWriteOnlyAccess"));

            var iamUpdateCircuitStatusLambdaRole = new Role(this,"UpdateCircuitStatusLambdaExecutionRole", new RoleProps
            {
                RoleName = "UpdateCircuitStatusLambdaExecutionRole",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
            });
            
            circuitBreakerTable.GrantReadWriteData(iamUpdateCircuitStatusLambdaRole);
            circuitBreakerTable.Grant(iamUpdateCircuitStatusLambdaRole, "dynamodb:DescribeTable");
            iamUpdateCircuitStatusLambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                Resources = new [] { "*" },
                Actions = new [] {
                    "logs:CreateLogGroup",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents"
                },
                Effect = Effect.ALLOW
            }));
            iamUpdateCircuitStatusLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayWriteOnlyAccess"));

            #if INCLUDE_TEST_FUNCTIONS
                var iamHelloWorldLambdaRole = new Role(this,"HelloWorldLambdaExecutionRole", new RoleProps
                {
                    RoleName = "HelloWorldLambdaExecutionRole",
                    AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
                });
                iamHelloWorldLambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                    Resources = new [] { "*" },
                    Actions = new [] {
                        "logs:CreateLogGroup",
                        "logs:CreateLogStream",
                        "logs:PutLogEvents"
                    },
                    Effect = Effect.ALLOW
                }));
                iamHelloWorldLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayWriteOnlyAccess"));

                var iamTestCircuitBreakerLambdaRole = new Role(this,"TestCircuitBreakerLambdaExecutionRole", new RoleProps
                {
                    RoleName = "TestCircuitBreakerLambdaExecutionRole",
                    AssumedBy = new ServicePrincipal("lambda.amazonaws.com")
                });
                iamTestCircuitBreakerLambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                    Resources = new [] { "*" },
                    Actions = new [] {
                        "logs:CreateLogGroup",
                        "logs:CreateLogStream",
                        "logs:PutLogEvents"
                    },
                    Effect = Effect.ALLOW
                }));
                iamTestCircuitBreakerLambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayWriteOnlyAccess"));
            #endif

            var iamStepFunctionRole = new Role(this,"step_functions_basic_execution", new RoleProps
            {
                RoleName = "step_functions_basic_execution",
                AssumedBy = new ServicePrincipal("states.amazonaws.com")
            });
            
            iamStepFunctionRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                Resources = new [] { "*" },
                Actions = new [] {
                    "logs:CreateLogGroup",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents"
                },
                Effect = Effect.ALLOW
            }));
            iamStepFunctionRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSXrayWriteOnlyAccess"));
            #endregion iamroles
            
            #region Lambda Functions

            var getCircuitStatusLambda = new Function(this,"GetCircuitStatusFunction", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                MemorySize = 256,
                Handler = "GetCircuitStatusLambda::GetCircuitStatusLambda.GetCircuitStatus::FunctionHandler",
                Role = iamGetCircuitStatusLambdaRole,
                Code = Code.FromAsset("lambdas/GetCircuitStatusLambda.zip"),
                Timeout = Duration.Seconds(30)
            });
            
            var updateCircuitStatusLambda = new Function(this,"UpdateCircuitStatusFunction", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                MemorySize = 256,
                Handler = "UpdateCircuitStatusLambda::UpdateCircuitStatusLambda.UpdateCircuitStatus::FunctionHandler",
                Role = iamUpdateCircuitStatusLambdaRole,
                Code = Code.FromAsset("lambdas/UpdateCircuitStatusLambda.zip"),
                Timeout = Duration.Seconds(30)
            });
            
            #if INCLUDE_TEST_FUNCTIONS
                var helloWorldLambda = new Function(this,"HelloWorldFunction", new FunctionProps
                {
                    Runtime = Runtime.DOTNET_6,
                    MemorySize = 256,
                    Handler = "HelloWorld::HelloWorld.Function::FunctionHandler",
                    Role = iamHelloWorldLambdaRole,
                    Code = Code.FromAsset("lambdas/HelloWorld.zip"),
                    Timeout = Duration.Seconds(30)
                });
                
                var testCircuitBreakerLambda = new Function(this,"TestCircuitBreakerFunction", new FunctionProps
                {
                    Runtime = Runtime.DOTNET_6,
                    MemorySize = 256,
                    Handler = "TestCircuitBreaker::TestCircuitBreaker.Function::FunctionHandler",
                    Role = iamTestCircuitBreakerLambdaRole,
                    Code = Code.FromAsset("lambdas/TestCircuitBreaker.zip"),
                    Timeout = Duration.Seconds(30)
                });
            #endif

            // Add code similar to that inside this #if to grant the State Machine permission to
            //  invoke the TargetLambda(s) for the protected services.
            #if INCLUDE_TEST_FUNCTIONS
                iamStepFunctionRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps {
                    Resources = new [] {
                        helloWorldLambda.FunctionArn,
                        testCircuitBreakerLambda.FunctionArn
                    },
                    Actions = new [] {
                        "lambda:InvokeFunction"
                    },
                    Effect = Effect.ALLOW
                }));
            #endif
            
            #endregion
            
            #region stepfunction
        
            var Success = new Succeed(this,"Success", new SucceedProps
            {
                OutputPath = "$.Payload"
            });

            var Fail = new Fail(this, "Fail");
            
            var getCircuitStatusTask = new LambdaInvoke(this, "Get Circuit Status", new LambdaInvokeProps
            {
                LambdaFunction = getCircuitStatusLambda,
                Comment = "Get Circuit Status",
                ResultSelector = new Dictionary<string, object> {
                    {"CircuitStatus.$", "$.Payload.CircuitControl.CircuitStatus"},
                    {"HiResTimeStamp.$", "$.Payload.CircuitControl.HiResTimeStamp"},
                    {"Count.$", "$.Payload.CircuitControl.Count"},
                    {"MaxAttempts.$", "$.Payload.CircuitControl.MaxAttempts"},
                    {"MaxConsecutive.$", "$.Payload.CircuitControl.MaxConsecutive"},
                    {"MaxHalfRate.$", "$.Payload.CircuitControl.MaxHalfRate"},
                    {"HalfIntervalSeconds.$", "$.Payload.CircuitControl.HalfIntervalSeconds"},
                    {"InactivityResetSeconds.$", "$.Payload.CircuitControl.InactivityResetSeconds"},
                    {"TimeoutSeconds.$", "$.Payload.CircuitControl.TimeoutSeconds"}
                },
                ResultPath = "$.CircuitControl",
                Payload = TaskInput.FromJsonPathAt("$")
            });

            var tryLimitReached = new Choice(this, "Try Limit Reached?")
                .When(Condition.Or(
                    Condition.IsNotPresent("$.CircuitControl.Count"),
                    Condition.NumberGreaterThanEqualsJsonPath("$.CircuitControl.Count", "$.CircuitControl.MaxAttempts")
                    ), Fail)
                .Otherwise(getCircuitStatusTask);

            var updateCircuitStatusTaskSuccess = new LambdaInvoke(this, "Record Invoke Success", new LambdaInvokeProps
            {
                LambdaFunction = updateCircuitStatusLambda,
                Comment = "Update CallResult",
                ResultPath = JsonPath.DISCARD,
                Payload = TaskInput.FromJsonPathAt("$")
            }).Next(Success);
            
            var updateCircuitStatusTaskFail = new LambdaInvoke(this, "Record Invoke Failure", new LambdaInvokeProps
            {
                LambdaFunction = updateCircuitStatusLambda,
                Comment = "Update CallResult",
                ResultSelector = new Dictionary<string, object> {
                    {"CircuitStatus.$", "$.Payload.CircuitControl.CircuitStatus"},
                    {"HiResTimeStamp.$", "$.Payload.CircuitControl.HiResTimeStamp"},
                    {"Count.$", "$.Payload.CircuitControl.Count"},
                    {"MaxAttempts.$", "$.Payload.CircuitControl.MaxAttempts"},
                    {"MaxConsecutive.$", "$.Payload.CircuitControl.MaxConsecutive"},
                    {"MaxHalfRate.$", "$.Payload.CircuitControl.MaxHalfRate"},
                    {"HalfIntervalSeconds.$", "$.Payload.CircuitControl.HalfIntervalSeconds"},
                    {"InactivityResetSeconds.$", "$.Payload.CircuitControl.InactivityResetSeconds"},
                    {"TimeoutSeconds.$", "$.Payload.CircuitControl.TimeoutSeconds"}
                },
                ResultPath = "$.CircuitControl",
                Payload = TaskInput.FromJsonPathAt("$")
            }).Next(tryLimitReached);
            
            var stateJson = new Dictionary<string, object>
            {
                {"Type", "Task"},
                {"Next", "Record Invoke Success"},
                {"Resource", "arn:aws:states:::lambda:invoke"},
                {"ResultPath", "$.Payload"},
                {
                    "Parameters", new Dictionary<string, object>
                    {
                        {"Payload.$", "$"},
                        {"FunctionName.$", "$.TargetLambda"}
                    }
                },
                {"Comment", "Task to execute lambda."},
                {"TimeoutSecondsPath", "$.CircuitControl.TimeoutSeconds"},
                {
                    "Catch", new []
                    {
                        new Dictionary<string, object>()
                        {
                            {"ErrorEquals", new string[] 
                                {
                                    "States.TaskFailed",
                                    "States.Timeout"
                                }},
                            {"Next", "Record Invoke Failure"},
                            {"ResultPath", "$.taskresult"}
                        }
                    }
                }
            };
            var executeLambdaTask = new CustomState(this, "Execute Lambda", new CustomStateProps
            {
                StateJson = stateJson
            }).Next(updateCircuitStatusTaskSuccess);

            var stepDefinition = Chain.Start(getCircuitStatusTask)
                .Next(new Choice(this, "Is Circuit Open?")
                    .When(Condition.StringEquals("$.CircuitControl.CircuitStatus", "OPEN"), Fail)
                    .Otherwise(executeLambdaTask)
                );

            LogGroup logGroup = new LogGroup(this, "CircutBreakerLogGroup");
         
            var stateMachine = new StateMachine(this, "CircuitBreaker-StepFunction", new StateMachineProps {
                StateMachineName = "CircuitBreaker-StepFunction",
                StateMachineType = StateMachineType.EXPRESS,
                Role = iamStepFunctionRole,
                TracingEnabled = true,
                Definition = stepDefinition,
                Logs = new LogOptions {
                    Destination = logGroup,
                    Level = LogLevel.ALL,
                    IncludeExecutionData = true
                }
            });
            
            #endregion
        }
    }
}
