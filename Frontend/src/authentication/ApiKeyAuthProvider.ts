// Custom provider for API Key based authentication
// adding the extra headers needed for the API
export default class ApiKeyAuthProvider {
    apiKeyHeder : string;
    apiKey : string;
    accessToken : string;  
    contentType: string;
    userEmail : string;
    userId: string;
  
    constructor(apiKeyHeder: string, apiKey: string, accessToken: string, userId: string, userEmail:string, contentType: string) {
      this.apiKeyHeder = apiKeyHeder;
      this.apiKey = apiKey;
      this.accessToken = accessToken;
      this.contentType = contentType;
      this.userEmail = userEmail;
      this.userId = userId;
    }
  
    AddAuthenticationInfo = async (config: any) => {
      if (!config.headers) {
        config.headers = {};
      }
        config.headers[this.apiKeyHeder] = this.apiKey;
        config.headers["Authorization"] =  "Bearer " + this.accessToken;       
        config.headers["userEmail"] =  this.userEmail;       
        config.headers["UserId"] =  this.userId ;          
        config.headers["Content-Type"] =  this.contentType;   
        return config;
    };
  }