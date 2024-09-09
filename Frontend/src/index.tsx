import { createRoot } from "react-dom/client";
import "./index.css";


// MSAL imports
import {
    PublicClientApplication,
    EventType,
    EventMessage,
    AuthenticationResult,
} from "@azure/msal-browser";
import App from "./components/App";
import { MsalConfiguration } from "./authentication/MsalConfiguration";


export const msalInstance = new PublicClientApplication(MsalConfiguration);

msalInstance.initialize().then(() => {
 
    // Account selection logic is app dependent. 
    // Adjust as needed for different use cases.
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
        msalInstance.setActiveAccount(accounts[0]);
    }

    msalInstance.addEventCallback((event: EventMessage) => {
        if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
            const payload = event.payload as AuthenticationResult;
            const account = payload.account;
            msalInstance.setActiveAccount(account);
        }
    });
   
    const container = document.getElementById("root");
    const root = createRoot(container!);
    root.render(<App pca={msalInstance} />);

});
