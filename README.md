## Implementing the circuit breaker pattern using AWS Step Functions and Amazon DynamoDB

The circuit breaker pattern can prevent a caller service from retrying a call to another service (callee) when the call has previously caused repeated timeouts or failures. The pattern is also used to detect when the callee service is functional again.

Use this pattern when:

* The caller service makes a call that is most likely going to fail.
* A high latency exhibited by the callee service (for example, when database connections are slow) causes timeouts to the callee service.
* The caller service makes a synchronous call, but the callee service isn't available or exhibits high latency.

To learn more about the Circuit Breaker pattern and other patterns, please refer to the Amazon Prescriptive Guidance page on [Cloud Design Patterns, architectures, and implementations](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/circuit-breaker.html)

## Architecture Overview

This example uses the [AWS Step Functions](https://aws.amazon.com/step-functions/), [AWS Lambda](https://aws.amazon.com/lambda/), and [Amazon DynamoDB](https://aws.amazon.com/dynamodb/) to implement the circuit breaker pattern:

![ALT](./asset/Architecture.png)

The Step Functions workflow provides circuit breaker capabilities. When a service wants to call another service, it starts the workflow with the name of the callee service.

The workflow gets the circuit status from the CircuitStatus DynamoDB table, which stores the currently degraded services. If the CircuitStatus contains an unexpired record for the service called, then the circuit is open. The Step Functions workflow returns an immediate failure and exit with a FAIL state.

If the CircuitStatus table does not contain an item for the called service or contains an expired record, then the service is operational. The ExecuteLambda step in the state machine definition invokes the Lambda function sent through a parameter value. The Step Functions workflow exits with a SUCCESS state, if the call succeeds.

The items in the DynamoDB table have the following attributes:

![ALT](./asset/payment.png)

*DynamoDB items list*

If the service call fails or a timeout occurs, the application retries with exponential backoff for a defined number of times. If the service call fails after the retries, the workflow inserts a record in the CircuitStatus table for the service with the CircuitStatus as OPEN, and the workflow exits with a FAIL state. Subsequent calls to the same service return an immediate failure as long as the circuit is open.

I enter the item with an associated ExpiryTimeStamp value to ensure eventual connection retries and the item expires at the defined time. The Get Circuit Status step in the state machine definition checks the service availability based on ExpiryTimeStamp value.

Expired items are deleted from the CircuitBreaker table using DynamoDB’s time to live (TTL) feature. DynamoDB’s time to live (TTL) allows you to define a per-item timestamp to determine when an item is no longer needed. I have defined the ExpiryTimeStamp as the TTL attribute.

At some point after the date and time of the ExpiryTimeStamp, [typically within 48 hours](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/howitworks-ttl.html), DynamoDB deletes the item from the CircuitBreaker table without consuming write throughput. DynamoDB determines the deletion time and there is no guarantee about when the deletion will occur.

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


## Prerequisites

For this walkthrough, you need:

- An [AWS](https://signin.aws.amazon.com/signin?redirect_uri=https%3A%2F%2Fportal.aws.amazon.com%2Fbilling%2Fsignup%2Fresume&client_id=signup) account and an AWS user with AdministratorAccess (see the [instructions](https://console.aws.amazon.com/iam/home#/roles%24new?step=review&commonUseCase=EC2%2BEC2&selectedUseCase=EC2&policies=arn:aws:iam::aws:policy%2FAdministratorAccess) on the [AWS Identity and Access Management](http://aws.amazon.com/iam) (IAM) console)
- Access to the following AWS services: [AWS Lambda](https://aws.amazon.com/lambda/), [AWS Step Functions](https://aws.amazon.com/step-functions/), and [Amazon DynamoDB](https://aws.amazon.com/dynamodb/).
- AWS SAM CLI using the instructions [here](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html).
- .NET 6.0 SDK installed
- [JetBrains Rider](https://www.jetbrains.com/rider/) or [Microsoft Visual Studio 2017](https://visualstudio.microsoft.com/) or later (or [Visual Studio Code](https://code.visualstudio.com/))


## Setting up the environment

Use the .NET 6.0 code in the GitHub repository and the AWS SAM template to create the AWS resources for this walkthrough. These include IAM roles, DynamoDB table, the Step Functions workflow, and Lambda functions.

You need an AWS access key ID and secret access key to configure the AWS Command Line Interface (AWS CLI). To learn more about configuring the AWS CLI, follow these [instructions](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-install.html).


## Method 1: Deploy using Serverless Application Model (AWS SAM)

### Step 1: Download the application

```bash
git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
```

After cloning, folder structure looks as below:

![ALT](./asset/files.png)


The [AWS Serverless Application Model (AWS SAM) CLI](https://aws.amazon.com/serverless/sam/) provides developers with a local tool for managing serverless applications on AWS.

The sam build command processes your AWS SAM template file, application code, and applicable language-specific files and dependencies. The command copies build artifacts in the format and location expected for subsequent steps in your workflow. Run these commands to process the template file:

### Step 2: Build the template

```bash
cd circuit-breaker
sam build
```

### Step 3: Deploy the application

```bash
sam deploy --guided
```

![ALT](./asset/sam.png)

You can also view the output in [AWS Cloudformation](https://aws.amazon.com/cloudformation/) page.

![ALT](./asset/Cloudformation.png)


## Method 2: Deploy using CDK

### Step 1: Download the application

```bash
git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
```

The Step Functions workflow provides the circuit-breaker function. Refer to the circuitbreaker.asl.json file in the statemachine folder for the state machine definition in the Amazon States Language (ASL).

### Step 2: Create packages of lambda functions

The Lambda functions in the circuit-breaker directory must be packaged and copied to the cdk-circuit-breaker/lambdas directory before deployment. Create the lambdas folder under the cdk-circuit-breaker directory. 

```bash
cd cdk-circuit-breaker
mkdir lambdas
cd ../ciruit-breaker
```

Run these commands to package the GetCircuitStatusLambda function and copy to the lambdas folder. 

```bash
cd circuit-breaker/GetCiruitStatus/src/GetCircuitStatusLambda
dotnet lambda package
cp bin/Release/net6.0/GetCircuitStatusLambda.zip ../../../../cdk-circuit-breaker/lambdas
cd ../../..
```

Repeat the above for all the lambda functions in the circuit-breaker directory


### Step 3: Deploy the CDK code

The `cdk.json` file tells the CDK Toolkit how to execute your app. It uses the [.NET CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project. Build and deploy the CDK code using the commands below.

```bash
npm install -g aws-cdk
cd cdk-circuit-breaker/src/CdkCircuitBreaker && dotnet build
cd ../..
cdk synth
cdk deploy
```


## Testing the service through the circuit breaker

The step functions workflow will be deployed by the SAM or CDK looks as below: 

![ALT](./asset/start.png)


To provide circuit breaker capabilities to the Lambda microservice, you must send the name or function ARN of the Lambda function to the Step Functions workflow:

## JSON
```
{
  "TargetLambda": "<Name or ARN of the Lambda function>"
}
```

## Successful run

To simulate a successful run, use the HelloWorld Lambda function provided by passing the name or ARN of the Lambda function the stack has created. Your input appears as follows:

## JSON
```
{
  "TargetLambda": "circuit-breaker-stack-HelloWorldFunction-xxxxxxxxxxxx"
}
```

During the successful run, the Get Circuit Status step checks the circuit status against the DynamoDB table. Suppose that the circuit is CLOSED, which is indicated by zero records for that service in the DynamoDB table. In that case, the Execute Lambda step runs the Lambda function and exits the workflow successfully.

![ALT](./asset/circuitclosed.png)

## Service timeout

To simulate a timeout, use the TestCircuitBreaker Lambda function by passing the name or ARN of the Lambda function the stack has created. Your input appears as:

## JSON
```
{
  "TargetLambda": "circuit-breaker-stack-TestCircuitBreakerFunction-xxxxxxxxxxxx"
}
```

Again, the circuit status is checked against the DynamoDB table by the Get Circuit Status step in the workflow. The circuit is CLOSED during the first pass, and the Execute Lambda step runs the Lambda function and timeout.

The workflow retries based on the retry count and the exponential backoff values, and finally returns a timeout error. It runs the Update Circuit Status step where a record is inserted in the DynamoDB table for that service, with a predefined time-to-live value specified by TTL attribute ExpireTimeStamp.

![ALT](./asset/Red.png)

The item in the DynamoDB table expires after 20 seconds, and the workflow retries the service again. This time, the workflow retries with exponential backoffs, and if it succeeds, the workflow exits successfully.

## Cleaning up

To avoid incurring additional charges, clean up all the created resources. Run the following command from a terminal window. This command deletes the created resources that are part of this example.

```bash
sam delete --stack-name circuit-breaker-stack --region <region name>
```
or

```bash
cdk destroy
```

This lab showed how to implement the circuit breaker pattern using Step Functions, Lambda, DynamoDB and .NET 6.0.  This pattern can help prevent system degradation in service failures or timeouts. 

Blog reference:- [https://aws.amazon.com/blogs/compute/using-the-circuit-breaker-pattern-with-aws-step-functions-and-amazon-dynamodb/](https://aws.amazon.com/blogs/compute/using-the-circuit-breaker-pattern-with-aws-step-functions-and-amazon-dynamodb/) 

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.

