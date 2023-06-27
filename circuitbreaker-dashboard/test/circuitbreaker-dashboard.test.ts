import { expect as expectCDK, matchTemplate, MatchStyle } from '@aws-cdk/assert';
import * as cdk from 'aws-cdk-lib';
//import { Template } from 'aws-cdk-lib/assertions';
import * as CircuitbreakerDashboard from '../lib/circuitbreaker-dashboard-stack';

test('Empty Stack', () => {
    const app = new cdk.App();
    // WHEN
    const stack = new CircuitbreakerDashboard.CircuitbreakerDashboardStack(app, 'MyTestStack');
    // THEN
    expectCDK(stack).to(matchTemplate({
      "Resources": {}
    }, MatchStyle.EXACT))
});