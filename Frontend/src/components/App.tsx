import "./App.css";

import { HashRouter as Router, Navigate, Route, Routes } from "react-router-dom";

import {
  FluentProvider,
  Spinner,
  teamsDarkTheme,
  teamsHighContrastTheme,
  teamsLightTheme,
} from "@fluentui/react-components";
import { useTeamsUserCredential } from "@microsoft/teamsfx-react";
import { IPublicClientApplication } from "@azure/msal-browser";

import Privacy from "../views/Privacy";
import TabConfig from "../views/TabConfig";
import TermsOfUse from "../views/TermsOfUse";
import Tab from "../views/Tab";
import { PageLayout } from "./PageLayout";
import { useEffect } from "react";
import { app } from "@microsoft/teams-js";
import { AppInsightsContext } from "@microsoft/applicationinsights-react-js";
import { reactPlugin } from "../applicationInsights/ApplicationInsightsService";
import { MsalProvider } from "@azure/msal-react";
import {config} from "../authentication/MsalConfiguration";
import { TeamsFxContext } from "./Context";



// Props for the App component
type AppProps = {
  pca: IPublicClientApplication;
};

/**
 * The main app which handles the initialization and routing
 * of the app.
 */
export default function App({ pca }: AppProps) {

  const { loading, theme, themeString, teamsUserCredential } = useTeamsUserCredential({    
    initiateLoginEndpoint: config.initiateLoginEndpoint!,
    clientId: config.clientId!,
   });

   
  useEffect(() => {   
    loading &&
      app.initialize().then(() => {
        console.log("app initialized");
        app.notifySuccess();
      }).catch((err) => {      
        // This fails outside of teams  
     });
  }, [loading]);
    
  return (
    <AppInsightsContext.Provider value={reactPlugin}>
      <MsalProvider instance={pca}>
        <TeamsFxContext.Provider value={{  theme, themeString, teamsUserCredential }}>
          <FluentProvider
            id="fluent-provider"
            theme={
              themeString === "dark"
                ? teamsDarkTheme
                : themeString === "contrast"
                ? teamsHighContrastTheme
                : teamsLightTheme
            }
          >
            <PageLayout>
              <Router>
                {loading ? (
                  <Spinner id="spinner" />
                ) : (
            
                    <Routes>
                      <Route path="/privacy" element={<Privacy />} />
                      <Route path="/termsOfUse" element={<TermsOfUse />} />
                      <Route path="/tab" element={<Tab />} />
                      <Route path="/config" element={<TabConfig />} />
                      <Route path="*" element={<Navigate to={"/tab"} />} />
                    </Routes>
                )}
              </Router>
            </PageLayout>
          </FluentProvider>
        </TeamsFxContext.Provider>
      </MsalProvider>
    </AppInsightsContext.Provider>
  );
}
