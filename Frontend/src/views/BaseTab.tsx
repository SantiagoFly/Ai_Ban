import React from "react";
import { AxiosInstance, createApiClient } from "@microsoft/teamsfx";
import { EventType } from "@azure/msal-browser";
import { app } from "@microsoft/teams-js";
import ApiKeyAuthProvider from "../authentication/ApiKeyAuthProvider";
import { TeamsFxContext } from "../components/Context";


export interface IBaseTabState {
    callbackId: string,  
    loading: boolean,  
    userId: string,
    userEmail: string,
    accessToken: string,
    authenticated: boolean,
    lastError: string,
}

// Base ms teams tab page
// Handle the user state and authentication for the tab
export default class BaseTab<TState extends IBaseTabState>  extends React.Component<any, TState> {

    apiClient: AxiosInstance | undefined;
    static readonly contextType = TeamsFxContext;
    context!: React.ContextType<typeof TeamsFxContext>
    
    // BasePage constructor
    constructor(props: any) {
        super(props);
        this.state = {
            callbackId: "",
            loading: true,
            userId: "",
            userEmail: "",
            accessToken: "",
            authenticated: false,          
           // data: {} as TState
        } as TState;
    }
     
    // Component initialization
    // Setups the callback for the login success event
    override async componentDidMount(): Promise<void> {

        //Handle the message event when authentication is successful with msal
        const callbackId = this.props.msalContext.instance.addEventCallback(async (message :any) => {     
            if (message.eventType === EventType.LOGIN_SUCCESS
                || message.eventType === EventType.ACQUIRE_TOKEN_SUCCESS) {
                try {
                    // Saves the user id and email on the state
                    
                    const msalInstance = this.props.msalContext.instance;            
                    const activeAccount = msalInstance.getActiveAccount();
                    const userId =  activeAccount?.localAccountId ?? ""; 
                    const userEmail = activeAccount?.username ?? "";  

                    const silentRequest = {
                        scopes: ["User.Read"], // replace with your actual scopes
                        account: activeAccount
                    };            
                    const response = await msalInstance.acquireTokenSilent(silentRequest);
                    const accessToken = response.accessToken;

                    this.setState({userId: userId, userEmail: userEmail, accessToken: accessToken, authenticated: true}, () => {
                        this.setupApiClient();
                        this.loadData();
                    });     

                    // Use the access token
                } catch (err) {
                    console.error(err);
                    // Handle error, possibly by acquiring token interactively
                }
          
            }
        });

        
        this.setState({callbackId: callbackId});

        // Tries to authenticate the user via Teams App
        try {
            let context = await app.getContext();
            if(context.user!=null){
                const userId =  context.user?.id ?? "";
                const userEmail =  context.user?.userPrincipalName ?? "";
                let teamsUserCredential = this.context.teamsUserCredential;                           
                let accessToken =  "";
                this.setState({userId: userId, userEmail: userEmail, accessToken: accessToken, authenticated: true}, () => {
                    this.setupApiClient();
                    this.loadData();
                });              
            }
        }
        catch (error) {
            // Unable to authenticate use msal    
            // tries to do th login
            this.setState({ lastError: JSON.stringify(error) });
            console.log(error);
            // try{
            //     let teamsUserCredential = this.context.teamsUserCredential;
            //     await teamsUserCredential?.login("");
            // }
            // catch (e) {
            //     this.setState({ lastError: JSON.stringify(error) });
            //     console.log(error);
            // }
          
        }
    }


    // This will be run on component unmount
    componentWillUnmount() {
        if (this.state.callbackId) {
            this.props.msalContext.instance.removeEventCallback(this.state.callbackId);
        }
    }

    // Setup the API client
    setupApiClient() : void {
        const apiKey  =  process.env.REACT_APP_API_KEY ?? "";;
        const apiHeader  = process.env.REACT_APP_API_HEADER ?? "";   
        const apiEndpoint  = process.env.REACT_APP_API_ENDPOINT ?? "";
        let accessToken = this.state.accessToken;
        const authProvider = new ApiKeyAuthProvider(apiHeader, apiKey, accessToken, this.state.userId, this.state.userEmail, "application/json");
        this.apiClient = createApiClient(apiEndpoint, authProvider);
        this.apiClient.defaults.timeout = 60000;
        this.apiClient.defaults.timeoutErrorMessage = "Ocurrió un error al invocar el servicio de backend";         
    }
    

    loadData() :  void {
    }
}
