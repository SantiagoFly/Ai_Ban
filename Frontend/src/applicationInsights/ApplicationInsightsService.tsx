import {ApplicationInsights, ITelemetryItem} from '@microsoft/applicationinsights-web';
import {ReactPlugin} from '@microsoft/applicationinsights-react-js';

const reactPlugin = new ReactPlugin();
const appInsights = new ApplicationInsights({
  config: {
    connectionString: "InstrumentationKey=801ec8ab-6ef0-4b24-b53a-2ab989842f4f;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=65c196b9-6b4e-43d3-8ffe-a6fadb23cc9c",
    //connectionString: "InstrumentationKey=6f93927e-4f7a-4b73-a0f5-a28db47c43a5;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/",
    extensions: [reactPlugin],
    enableAutoRouteTracking: true,
    disableAjaxTracking: true,
    autoTrackPageVisitTime: true,  
    enableCorsCorrelation: true,
    enableRequestHeaderTracking: true,
    enableResponseHeaderTracking: true,
    enableDebug: true,
    disableExceptionTracking: false,
  }
});

appInsights.loadAppInsights();

appInsights.addTelemetryInitializer((env:ITelemetryItem) => {
    env.tags = env.tags || [];
});

export { reactPlugin, appInsights };