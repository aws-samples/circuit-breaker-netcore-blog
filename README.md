## Implementing the circuit breaker pattern using AWS Step Functions and Amazon DynamoDB

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.


## Prerequisites:

-	An AWS account
-	An AWS user with AdministratorAccess (see the instructions on the AWS Identity and Access Management (IAM) console)
-	Access to the following AWS services: AWS Lambda, AWS Step Functions, and Amazon DynamoDB
-	.NET Core 3.1 SDK installed
-	JetBrains Rider or Microsoft Visual Studio 2017 or later (or Visual Studio Code)


## Deploy using SAM

### Step 1: Download the application

```shell
$ git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
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

## Deploy using CDK

### Step 1: Download the application

```shell
$ git clone https://github.com/aws-samples/circuit-breaker-netcore-blog.git
```

### Step 2: Create packages of lambda functions

The Lambda functions in the circuit-breaker directory must be packaged and copied to the cdk-circuit-breaker\lambdas directory before deployment. Run these commands to process the GetCircuitStatusLambda function:

```shell
$ cd circuit-breaker-src/GetCircuitStatusLambda/src/GetCircuitStatusLambda
$ dotnet lambda package
$ cp bin/Release/netcoreapp3.1/GetCircuitStatusLambda.zip ../../../../cdk-circuit-breaker/lambdas
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
