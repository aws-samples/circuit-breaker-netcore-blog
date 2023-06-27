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

import * as cdk from 'aws-cdk-lib';
import { aws_cloudwatch as cloudwatch } from 'aws-cdk-lib';
import { GraphWidget, Metric, DimensionHash } from 'aws-cdk-lib/aws-cloudwatch'

export interface MyStackProps extends cdk.StackProps {
  functions?: any;
}

export class CircuitbreakerDashboardStack extends cdk.Stack {
  constructor(scope: cdk.App, id: string, props: MyStackProps = {}) {
    super(scope, id, props);

    const CircuitBreakerDashboard = new cloudwatch.Dashboard(this, 'CircuitBreaker-Dashboard', { dashboardName: 'CircuitBreaker-Dashboard' })
    CircuitBreakerDashboard.addWidgets(
      this.buildAllGraphWidget('All Target Lambda Functions', props.functions),
      this.buildClosedGraphWidget('Circuit Breaker CLOSED', props.functions),
      this.buildHalfGraphWidget('Circuit Breaker HALF', props.functions),
      this.buildOpenGraphWidget('Circuit Breaker OPEN', props.functions),
      this.buildTargetLambdaGraphWidget('Status for ' + props.functions.TestCircuitBreakerFunction, props.functions.GetCircuitStatusFunction, props.functions.TestCircuitBreakerFunction),
      this.buildTargetLambdaGraphWidget('Status for ' + props.functions.HelloWorldFunction, props.functions.GetCircuitStatusFunction, props.functions.HelloWorldFunction)
    );
  }

  // Example with Graph Widget
  buildAllGraphWidget(widgetName: string, functions: any = {}): GraphWidget {
    return new GraphWidget({
      title: widgetName,
      height: 7,
      width: 6,
      left: [
        this.buildMetric('CircuitBreaker', 'CLOSED', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'CLOSED' }),
        this.buildMetric('CircuitBreaker', 'HALF', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'HALF' }),
        this.buildMetric('CircuitBreaker', 'OPEN', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'OPEN' })
      ],
      stacked: true,
      liveData: true
    })
  }

  // Example with Graph Widget
  buildClosedGraphWidget(widgetName: string, functions: any = {}): GraphWidget {
    return new GraphWidget({
      title: widgetName,
      height: 7,
      width: 6,
      left: [
        this.buildMetric('CircuitBreaker', 'CLOSED', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'CLOSED' })
      ],
      stacked: true,
      liveData: true
    })
  }

  buildHalfGraphWidget(widgetName: string, functions: any = {}): GraphWidget {
    return new GraphWidget({
      title: widgetName,
      height: 7,
      width: 6,
      left: [
        this.buildMetric('CircuitBreaker', 'HALF', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'HALF' })
      ],
      stacked: true,
      liveData: true
    })
  }

  // Example with Graph Widget
  buildOpenGraphWidget(widgetName: string, functions: any = {}): GraphWidget {
    return new GraphWidget({
      title: widgetName,
      height: 7,
      width: 6,
      left: [
        this.buildMetric('CircuitBreaker', 'OPEN', 'CircuitStatus', 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'OPEN' })
      ],
      stacked: true,
      liveData: true
    })
  }
  
 // Example with Graph Widget
  buildTargetLambdaGraphWidget(widgetName: string, serviceName: string = '', targetLambda: string = ''): GraphWidget {
    return new GraphWidget({
      title: widgetName,
      height: 7,
      width: 12,
      left: [
        this.buildMetric('CircuitBreaker', 'CLOSED', serviceName, 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'CLOSED', ServiceType: 'AWS::Lambda::Function', ServiceName: serviceName, TargetLambda: targetLambda}),
        this.buildMetric('CircuitBreaker', 'HALF', serviceName, 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'HALF', ServiceType: 'AWS::Lambda::Function', ServiceName: serviceName, TargetLambda: targetLambda}),
        this.buildMetric('CircuitBreaker', 'OPEN', serviceName, 'CircuitBreakerUsage', 'sum', cdk.Duration.minutes(5), {  CircuitStatus: 'OPEN', ServiceType: 'AWS::Lambda::Function', ServiceName: serviceName, TargetLambda: targetLambda})
      ],
      stacked: true,
      liveData: true
    })
  }

  buildMetric(namespace: string, label: string, functionName: string, metricName: string, statistic: string, period: any, dimensions: DimensionHash): Metric {
    return new Metric({
      namespace: namespace,
      metricName: metricName,
      period: period,
      dimensionsMap: dimensions,
      label: label,
      statistic: statistic
    })
  }
}
