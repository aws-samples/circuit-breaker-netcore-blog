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

import { CloudFormation } from "@aws-sdk/client-cloudformation";

export class CloudFormationHelper {
    cfn_parser = async (stack_name: string = '') => {
        let functions: any = {}
        try {
          const cfn = new CloudFormation({ region: process.env["DEPLOY_REGION"]?.toString().toLocaleLowerCase() })
            const data = await cfn.describeStackResources({ StackName: stack_name })
            if (data.StackResources) {
                data.StackResources.forEach(resource => {
                    // Use of "startsWith" instead of "==="" is to support both SAM and CDK
                    if (resource.ResourceType === "AWS::Lambda::Function")
                    {
                        if (resource.LogicalResourceId?.startsWith("GetCircuitStatusFunction")) functions.GetCircuitStatusFunction = resource.PhysicalResourceId
                        if (resource.LogicalResourceId?.startsWith('TestCircuitBreakerFunction')) functions.TestCircuitBreakerFunction = resource.PhysicalResourceId
                        if (resource.LogicalResourceId?.startsWith('HelloWorldFunction')) functions.HelloWorldFunction = resource.PhysicalResourceId    
                    }

                });
            }
        }
        catch (err) {
            throw new Error("Unable to access your AWS SAM backend CloudFormation stack_name: " + stack_name + "; " + String(err))
        }
        return functions
    }
}