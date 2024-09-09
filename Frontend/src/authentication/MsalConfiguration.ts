import { Configuration, PopupRequest } from "@azure/msal-browser";

// Config object to be passed to Msal on creation
export const MsalConfiguration: Configuration = {
    auth: {
        clientId: process.env.REACT_APP_CLIENT_ID!,
        authority: process.env.REACT_APP_OAUTH_AUTHORITY, 
        redirectUri: "/",
        postLogoutRedirectUri: "/",        
    },
    system: {
         // Disables WAM Broker
        allowNativeBroker: false
    }
};



// Add here scopes for id token to be used at MS Identity Platform endpoints.
export const loginRequest: PopupRequest = {
    scopes: ["User.Read"],
    prompt: 'select_account',
};


// Add here the endpoints for MS Graph API services you would like to use.
export const graphConfig = {
    graphMeEndpoint: "https://graph.microsoft.com/v1.0/me"
};


export const config = {
    initiateLoginEndpoint: process.env.REACT_APP_START_LOGIN_PAGE_URL,
    clientId: process.env.REACT_APP_CLIENT_ID,
    apiEndpoint:"",
    apiName: "",
  };
  