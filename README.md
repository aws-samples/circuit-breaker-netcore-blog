> “**Legal Disclaimer:** The sample code; software libraries; command line tools; proofs of concept; templates; or other related technology (including any of the foregoing that are provided by our personnel) is provided to you as AWS Content under the AWS Customer Agreement, or the relevant written agreement between you and AWS (whichever applies). You should not use this AWS Content in your production accounts, or on production or other critical data. You are responsible for testing, securing, and optimizing the AWS Content, such as sample code, as appropriate for production grade use based on your specific quality control practices and standards. Deploying AWS Content may incur AWS charges for creating or using AWS chargeable resources, such as running Amazon EC2 instances or using Amazon S3 storage.”

## Implementing the circuit breaker pattern using AWS Step Functions and Amazon DynamoDB

Blog reference:- [https://aws.amazon.com/blogs/compute/using-the-circuit-breaker-pattern-with-aws-step-functions-and-amazon-dynamodb/](https://aws.amazon.com/blogs/compute/using-the-circuit-breaker-pattern-with-aws-step-functions-and-amazon-dynamodb/) 

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.

## Description

The Step Functions workflow provides circuit breaker capabilities. When a service wants to call another service, it starts the workflow with the name of the callee service. The workflow gets the circuit status from the CircuitStatus DynamoDB table, which stores the currently degraded services. If the CircuitStatus contains an unexpired record for the service called, then the circuit is open. The Step Functions workflow returns an immediate failure and exit with a FAIL state.

If the CircuitStatus table does not contain an item for the called service or contains an expired record, then the service is operational. The ExecuteLambda step in the state machine definition invokes the Lambda function sent through a parameter value. The Step Functions workflow exits with a SUCCESS state, if the call succeeds.

If the service call fails or a timeout occurs, the application retries with exponential backoff for a defined number of times. If the service call fails after the retries, the workflow inserts a record in the CircuitStatus table for the service with the CircuitStatus as OPEN, and the workflow exits with a FAIL state. Subsequent calls to the same service return an immediate failure as long as the circuit is open.

I enter the item with an associated **ExpiryTimeStamp** value to ensure eventual connection retries. I get the currently degraded services in *GetCircuitStatusLambda* function by querying services with ExpiryTimeStamp in the future. Services with ExpiryTimeStamp greater than current time are currently degraded and will not be retried.

```cs
var serviceDetails = _dbContext.QueryAsync<CircuitBreaker>(serviceName, QueryOperator.GreaterThan,
                new List<object>
                    {currentTimeStamp}).GetRemainingAsync();
```

### TTL feature for deleting expired items

DynamoDB's TTL feature is used to delete the expired items from the CircuitBreaker table. DynamoDB’s time to live (TTL) allows you to define a per-item timestamp to determine when an item is no longer needed. I have defined the ExpiryTimeStamp as the TTL attribute. At some point after the date and time of the ExpiryTimeStamp, typically within 48 hours, DynamoDB deletes the item from the CircuitBreaker table without consuming write throughput. DynamoDB determines the deletion time and there is no guarantee about when the deletion will occur. 


## Prerequisites:

-	An AWS account
-	An AWS user with AdministratorAccess (see the instructions on the AWS Identity and Access Management (IAM) console)
-	Access to the following AWS services: AWS Lambda, AWS Step Functions, and Amazon DynamoDB
-	.NET 6.0 SDK installed
-	JetBrains Rider or Microsoft Visual Studio 2017 or later (or Visual Studio Code)
-   To work with the AWS CDK, you must have an AWS account and credentials and have installed Node.js and the AWS CDK Toolkit



## Deploy using SAM

### Step 1: Download the application

```shell
$ git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
$ cd circuit-breaker-netcore-blog
```

### Step 2: Build the template

```shell
$ cd circuit-breaker
$ sam build
```

### Step 3: Deploy the application

```shell
$ sam deploy --guided
```

### Step 4: Delete the SAM application when done

Note:  Evaluation of the Circuit Breaker Dashboard requires the application to be deployed.

```shell
$ sam delete
```

## Deploy using CDK

### Step 1: Download the application

```shell
$ git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
$ cd circuit-breaker-netcore-blog
```

### Step 2: Create packages of lambda functions

The Lambda functions in the circuit-breaker directory must be packaged and copied to the cdk-circuit-breaker\lambdas directory before deployment. 

First create the lambdas subdirectory where the excecutable code zip files will be stored for the build.

```shell
$ mkdir cdk-circuit-breaker/lambdas
```

Run these commands to process the GetCircuitStatusLambda function:

```shell
$ cd circuit-breaker/GetCircuitStatusLambda/src/GetCircuitStatusLambda
$ dotnet lambda package
$ cp bin/Release/net6.0/GetCircuitStatusLambda.zip ../../../../cdk-circuit-breaker/lambdas
```

Repeat the same commands for all the Lambda functions in the circuit-breaker-src directory.

### Step 3: Deploy the CDK code

The `cdk.json` file tells the CDK Toolkit how to execute your app. It uses the [.NET Core CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project. Build and deploy the CDK code using the commands below.

```shell
$ npm install -g aws-cdk
$ cd cdk-circuit-breaker/src/CdkCircuitBreaker && dotnet build
$ cd ../..
$ cdk synth
$ cdk deploy
```

### Step 4: Delete the CDK application when done

Note:  Evaluation of the Circuit Breaker Dashboard requires the application to be deployed.

```shell
$ cdk destroy
```

## Deploy the CloudWatch Dashboard

CircuitBreaker-Dashboard is a custom CloudWatch dashboard that provides operational observability for Circuit Breaker.  Metrics used are provided by the GetCircuitStatus Lambda function via Embedded Metric Format (EMF) records in the function's CloudWatch Logs.

### Prerequisites
You must have the SAM or CDK version of the Circuit Breaker application installed to install and use the dashboard.  The instructions assume you have already downloaded the code as per Step 1 in the directions for installing the application using SAM or CDK.

### Step 1: Install, Build and Deploy the dashboard

```shell
cd <top-level directory for Circuit Breaker Dashboard cdk project>
npm outdated
npm update --force
npm install --force
npm install -g typescript aws-cdk --force
```

Note: Use the name of the Circuit-Breaker CloudFormation Stack for the stack_name.
```shell
cdk deploy -c stack_name=<name of circuit-breaker stack>
```

The custom dashboard can be viewed in the AWS console under the CloudWatch service.  Click on "Dashboards" in the menu on the left and then select CircuitBreaker-Dashboard.  This dashboard can be customized to have separate Graph Widgets for each of your Circuit Breaker protected services.  These would replace the lower widgets that are provided for TestCircuitBreakerFunction and HelloWorldFunction.  The code to modify is in lib/circuitbreaker-dashboard-stack.ts and lib/helper/cloudformation-parser.ts.

The Graph Widgets at the top of the dashboard provide an aggregate  view for all services protected by the Circuit Breaker workflow.

### Step 2: To delete the Dashboard stack
```shell
cdk destroy -c stack_name=<name of circuit-breaker stack>
```