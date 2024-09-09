import React, { ChangeEvent  }  from "react";
import { Image, Button, ButtonProps, Card, CardPreview, Divider, Input, InputOnChangeData, Spinner, Text, tokens, Dialog, DialogTrigger, DialogSurface, DialogBody, DialogTitle, DialogContent, Textarea, DialogActions } from "@fluentui/react-components";
import { reactPlugin } from "../applicationInsights/ApplicationInsightsService";
import { withMsal } from "@azure/msal-react";
import { withAITracking } from "@microsoft/applicationinsights-react-js";
import BaseTab, { IBaseTabState } from "./BaseTab";
import { LoginButton } from "../components/Login";
import { BinRecycle24Regular, Dismiss16Regular, Send16Regular, ThumbDislike16Filled, ThumbDislike16Regular, ThumbLike16Filled, ThumbLike16Regular,HistoryRegular   } from "@fluentui/react-icons";
import { QueryResultModel } from "../models/QueryResultModel";
import { ConversationResultModel } from "../models/ConversationResultModel";
import { ChatMessageModel, LikeState } from "../models/ChatMessageModel";
import { StartUpOptionModel } from "../models/StartUpOptionModel";
import ChatHistory from "../components/ChatHistory";
import ChatWelcome from "../components/ChatWelcome";
import { QueryRequestModel } from "../models/QueryRequestModel";
import moment from "moment";
import Markdown from "react-markdown";
import rehypeExternalLinks from "rehype-external-links";
import { Drawer, DrawerBody, DrawerHeader, DrawerProps } from "@fluentui/react-components";
import { Tooltip } from "@fluentui/react-components";


// Tab state
type TabState = IBaseTabState & {
  prompt: string, 
  queryResults: QueryResultModel[],
  currentChat: ChatMessageModel[],
  currentConversation?: string,
  chatHistory: ConversationResultModel[],
  startUpOption: StartUpOptionModel[],
  loading: boolean,
  scrollToLast: boolean,
  comment: string,
  commentedRequestId: string,
  isCommentDialogOpen: boolean,
  chatListRef: React.MutableRefObject<HTMLUListElement | null>,
  callbackId?: string,
  initializing: boolean,
  lastError: string,
  isDrawerOpen: boolean,
}


// Tab components to handle the chats
export class Tab extends BaseTab<TabState> {

  constructor(props: any) {
    super(props);
    this.state = { 
      prompt: "",
      callbackId: "",
      loading: true,
      userId: "",
      userEmail: "",
      accessToken: "",
      authenticated: false,      
      scrollToLast: false,
      chatHistory:  [],   
      currentConversation: "",
      queryResults: [],
      currentChat: [],
      startUpOption :[],
      isCommentDialogOpen: false,
      chatListRef: React.createRef<HTMLUListElement>(),
      comment: "",
      commentedRequestId: "",
      initializing: true,     
      lastError: "",
      isDrawerOpen: false
    };    
  }
 

  override loadData(): void {
    this.ShowWelcomeMessage();
  }

  // Clear the prompt
  clearPrompt(): void {
    this.setState({ prompt: "" });
  }


  // Clear the messages
  clearChatMessages(): void {  
    this.setState({ currentConversation: "", currentChat: [] });
  }

  // Show an error message
  showErrorMessage(err: string): void {
    this.addMessageToChat("00000000-0000-0000-0000-000000000000", "bot", 
    "Lo sentimos, pero ocurrió un error con el servicio (" + err + ")", false, false, 0, 0, "", [], 
    LikeState.None, moment());   
  }


  // Handle the enter
  onInputChange =(ev: ChangeEvent<HTMLInputElement>, data: InputOnChangeData) =>{
    this.setState({ prompt: data.value, queryResults: this.state.queryResults });   
  };

  
  // Shows the welcome message, loading the startup option and last conversations
  ShowWelcomeMessage() : void {
      this.apiClient?.get("startup/options", ).then((response: any) => {
        let result = response.data.data;   
        this.setState({ startUpOption: result, loading: false});         
      }).catch((err: any) => {
        this.showErrorMessage(err);
      });
      
      this.apiClient?.get("chat/history", ).then((response: any) => {
        if (response?.status === 200) {
          let result = response.data.data;   
          this.setState({ chatHistory: result, initializing: false ,isDrawerOpen: false});           
        }
        else
        {
          this.showErrorMessage(response?.status);
        }
      }).catch((err: any) => {
        this.showErrorMessage(err);
      })             
  }

  // Reloads chat history
  reloadChatHistory(): void {
    this.apiClient?.get("chat/history").then((response: any) => {
      if (response?.status === 200) {
        let result = response.data.data;   
        this.setState({ chatHistory: result, initializing: false });           
      }
      else
      {
        this.showErrorMessage(response?.status);
      }
    }).catch((err: any) => {
      this.showErrorMessage(err);
    });
  }


  // Return the search button
  SearchButton: React.FC<ButtonProps> = (props) => {
    return (
        <div>{
          this.state.loading ? 
            <Spinner size="tiny"/> : 
            <div>
              <Button
                  {...props}
                  appearance="transparent"
                  icon={<Dismiss16Regular />}
                  size="small"
                  onClick={() => this.clearPrompt()}
              />
            <Button
                  {...props}
                  appearance="transparent"
                  icon={<Send16Regular />}
                  size="small"
                  onClick={() => this.queryReports(this.state?.prompt)}
              />
            </div>       
        }
       </div>         
    );
  };
  

  // New  button component
  NewChatButton: React.FC<ButtonProps> = (props) => {
    return (
      <Tooltip content="Limpia los mensajes del chat actual y comienza una nueva conversación con Bancard AI" relationship="label">
        <Button
          {...props}
          appearance="transparent"
          size="large"
          icon={<BinRecycle24Regular />}
          onClick={() => {this.clearChatMessages(); this.reloadChatHistory();}}
        />
      </Tooltip>
    );
  };

  // Drawer button component
  DrawerButton: React.FC<ButtonProps> = (props) => {
    return (
      <Tooltip content="Muestra el historial de conversaciones pasadas" relationship="label">
        <Button
          {...props}
          appearance="transparent"
          size="large"
          icon={<HistoryRegular />}
          onClick={() => this.setState({ isDrawerOpen: true })}
        />
      </Tooltip>
    );
  };

  // Call the API to like or dislike a search result
  async likeOrDislikeSearchResult(requestId: string, conversationId: string, liked: boolean, comment:string ) 
  {
    const response = await this.apiClient?.post("query/like",  { requestId, conversationId, liked, comment });  
    if (response?.status == 200) {
      return true;
    }
    else{
      console.log("Error " + response?.status);
      return false;
    }
  }


  // Adds a message to the chat list
  addMessageToChat(requestId: string, role: string, message: string, 
      canLike: boolean, isLoading: boolean, tokens: number, requestTime: number,
      backendVersion: string, suggestedQuestions: string[], likeState: LikeState, date: moment.Moment) : void{

      let time = date.date() === moment().date() ? date.format('HH:mm') : date.format('DD/MM/YYYY HH:mm'); 
      this.state.currentChat.push({ 
        "requestId": requestId,
        "role": role,  
        likeState: likeState,
        canLike: canLike,
        "date": time, 
        "message": message, 
        "suggestedQuestions": [],
        "requestTime": requestTime,
        backendVersion:backendVersion,
        tokens: tokens});
      this.setState({  loading: isLoading }, ()=>
      {
        this.state?.chatListRef.current?.lastElementChild?.scrollIntoView();      
     });          
  }
     
  
  // Performs the document search
  async queryReports(prompt: string): Promise<void> { 
    if (prompt!=null && prompt.length > 0)
    {      
        this.addMessageToChat("00000000-0000-0000-0000-000000000000", "user", prompt, false, true, 0, 0, "", [], LikeState.None, moment());
    
        this.addMessageToChat("00000000-0000-0000-0000-000000000001", "bot", 
          `Consultando...`, false, true, 0, 0, "", [], LikeState.None, moment());  

        const queryRequestBody: QueryRequestModel = { "Prompt": prompt };
        if (this.state.currentConversation){
          queryRequestBody.ConversationId = this.state.currentConversation;       
        }
        // Removes the last message
        this.state.currentChat.pop();
        
        // Fetch suggested prompt
        const suggestedResponse = await this.apiClient?.post("query/suggested", queryRequestBody)
            .catch((err: any) => {
                this.showErrorMessage(err);
            });
        
        let suggestedPrompt = '';

        if (suggestedResponse?.status === 200 && suggestedResponse?.data?.success) {
          suggestedPrompt = suggestedResponse.data.data.suggestedPrompt;
          this.addMessageToChat("00000000-0000-0000-0000-000000000002", "bot", `Consultando: **${suggestedPrompt}**`, false, true, 0, 0, "", [], LikeState.None, moment());
        
          queryRequestBody.Prompt = suggestedPrompt;
          const response = await this.apiClient?.post("query", queryRequestBody)
            .catch((err: any) => {
              this.showErrorMessage(err);
            });

          // Removes the last message
          this.state.currentChat.pop();

          if (response?.status === 200 && response?.data?.success) {
            let  result = response.data.data;
            this.setState({ currentConversation: result.conversationId });
            const responseText = result.summary;
            const finalMessage = `**${suggestedPrompt}**\n\n${responseText}`;
            this.addMessageToChat(result.requestId, "bot", finalMessage,  true, false, 
              result.tokens, result.requestTime, result.backendVersion, result.suggestedQuestions, LikeState.None, moment());    
            this.setState({ prompt: "", queryResults: [result] });  
          }
          else
          {
            this.addMessageToChat("00000000-0000-0000-0000-000000000000", "bot", 
              "Lo sentimos, pero ocurrió un error al conectar con el servicio de AI, por favor, intenta nuevamente", false, false, 0, 0, "", [], LikeState.None, moment());   
          }
      } else {
          this.addMessageToChat("00000000-0000-0000-0000-000000000002", "bot", 
              "Lo sentimos, pero ocurrió un error al conectar con el servicio de AI, por favor, intenta nuevamente", false, false, 0, 0, "", [], LikeState.None, moment());   
      }
    }
  };


  // Format the message as HTML
  formatMessageAsHtml(message: string) : string  {
    if (!message) return "";
    let htmlMessage = message.replace(/(\n\n)/g, "\n");
    htmlMessage = htmlMessage.replace(/(\r\n|\r|\n)/g, '<br>');
    return htmlMessage;
  };


  
  // Like button component
  LikeButton: React.FC<ChatMessageModel> = (props) => (
    <div>
      <Dialog open={this.state?.isCommentDialogOpen}
        onOpenChange={(e, data) => this.setState({ isCommentDialogOpen: data.open, commentedRequestId: props.requestId })}>
        <DialogTrigger disableButtonEnhancement>
          <Button
            appearance="transparent"
            icon={props.likeState === LikeState.Like ?
              <ThumbLike16Filled primaryFill={tokens.colorBrandForegroundInvertedHover} /> : <ThumbLike16Regular />}
            onClick={async () => {
              const previousState = props.likeState;
              const conversationId = this.state.currentConversation;
              props.likeState = LikeState.Like;
              try {
                if (await this.likeOrDislikeSearchResult(props.requestId, conversationId || "", true, "")) {
                  props.likeState = LikeState.Like;
                  this.updateConversationMessageLikeState(conversationId || '', props.requestId, props.likeState);
                } else {
                  props.likeState = previousState;
                }
              } catch (error) {
                props.likeState = previousState;
              }
              this.forceUpdate();
            }}
            size="small" />
        </DialogTrigger>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Enviar un comentario</DialogTitle>
            <DialogContent>
              <Textarea value={this.state?.comment} appearance="outline" style={{ width: "100%", height: "100px" }}
                placeholder="Indícanos como podríamos mejorar la calidad de respuesta"
                maxLength={500}
                resize="both"
                onChange={(ev, data) => this.setState({ comment: data.value })} />
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Cerrar</Button>
              </DialogTrigger>
              <Button
                {...props}
                appearance="primary"
                onClick={async () => {
                  const conversationId = this.state.currentConversation || "";
                  const result = await this.likeOrDislikeSearchResult(this.state?.commentedRequestId, conversationId, true, this.state?.comment);
                  if (result) {
                    // find and update the like state of the proper request
                    this.setState({ isCommentDialogOpen: false, comment: "", commentedRequestId: "" });
                    const chatElement = this.state?.currentChat.find((c: ChatMessageModel) => c.requestId === this.state?.commentedRequestId);
                    if (chatElement != null) {
                      chatElement.likeState = LikeState.Like;
                      this.updateConversationMessageLikeState(conversationId, chatElement.requestId, props.likeState);
                      this.forceUpdate();
                    }
                  }
                }}>
                Enviar comentario
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );

    // Dislike  button component
    DislikeButton: React.FC<ChatMessageModel> = (props) => (
      <div>{<Dialog open={this.state?.isCommentDialogOpen}
        onOpenChange={(e, data) => this.setState({ isCommentDialogOpen: data.open, commentedRequestId: props.requestId })}>
        <DialogTrigger disableButtonEnhancement>
          <Button
            appearance="transparent"
            icon={props.likeState === LikeState.Dislike ?
              <ThumbDislike16Filled primaryFill={tokens.colorBrandForegroundInvertedHover} /> : <ThumbDislike16Regular /> }
              onClick={ async () => {
                const previousState = props.likeState;
                const conversationId = this.state.currentConversation;
                props.likeState = LikeState.Dislike;
                try {
                  if (await this.likeOrDislikeSearchResult(props.requestId, conversationId || "", false, "")){
                    props.likeState = LikeState.Dislike;
                    this.updateConversationMessageLikeState(conversationId || '', props.requestId, props.likeState);
                  } else {
                    props.likeState = previousState;  
                  }  
                } catch (error) {
                  props.likeState = previousState;  
                }
                this.forceUpdate();
              }}
            size="small" />
        </DialogTrigger>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Enviar un comentario</DialogTitle>
            <DialogContent>
              <Textarea value={this.state?.comment} appearance="outline" style={{ width: "100%", height: "100px" }}
                placeholder="Indícanos como podríamos mejorar la calidad de respuesta"
                maxLength={500}
                resize="both"
                onChange={(ev, data) => this.setState({ comment: data.value })} />
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Cerrar</Button>
              </DialogTrigger>
              <Button
                {...props}
                appearance="primary"
                onClick={async () => {
                  const conversationId = this.state.currentConversation || "";
                  const result = await this.likeOrDislikeSearchResult(this.state?.commentedRequestId, conversationId, false, this.state?.comment);
                  if (result) {
                    // find and update the like state of the proper request
                    this.setState({ isCommentDialogOpen: false, comment: "", commentedRequestId: "" });
                    const chatElement = this.state?.currentChat.find((c: ChatMessageModel) => c.requestId === this.state?.commentedRequestId);
                    if (chatElement != null) {
                      chatElement.likeState = LikeState.Dislike;
                      this.updateConversationMessageLikeState(conversationId, chatElement.requestId, props.likeState);
                      this.forceUpdate();
                    }
                  }
                } }>
                Enviar comentario
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>}
      </div>
    );
    



  // Updates 
  updateConversationMessageLikeState(conversationId: string, requestId: string, likeState: LikeState) {
    const conversation = this.state.chatHistory.find( conversation => conversation.conversationId === conversationId);
    const request = conversation?.history.find( historyRequest => historyRequest.requestId === requestId);
    if (!request){
      return;
    }
    request.liked = likeState;    
    const newChatHistory = JSON.parse(JSON.stringify(this.state.chatHistory));
    this.setState({chatHistory: newChatHistory});
  }

  
  // Component render
  render() {
    return (
        this.state.authenticated ? 
          <div style={{
            display: "grid",
            height: "100vh",
            backgroundColor: tokens.colorNeutralBackground4,
            gridTemplateColumns: "auto"
          }}>  
       
          {/* Main panel with the chat */}
          <div style={{display: "grid",height: "100vh", gridTemplateRows: "auto 32px",  padding: '0px 48px 52px 24px'}}>   
          
             {this.state?.currentChat.length === 0 ? (
                // Render the welcome message component
                <div style={{display:"flex", justifyContent:"center", alignItems:"center"}}>
                 <ChatWelcome actions={this.state?.startUpOption} 
                      onActionSelected={(action) => { this.queryReports(action.text); }}
                  /> 
                </div>
              ):( 
                  // Render the chat component
                  <ul ref={this.state?.chatListRef} 
                    style={{listStyle:"none",padding: "0px", overflowY:"scroll", 
                      scrollbarWidth:"none", scrollBehavior:"smooth"}}>{
                        this.state?.currentChat.map((t: ChatMessageModel) => {     
                          return (
                            <div key={`${t.requestId}-${t.role}`}>
                               {t.role == "bot" ? (
                                <li style={{
                                  overflow:"hidden",
                                  margin: "0px 80px 0px 0px",
                                  display:"flex"}}>
                                  <div>                                                        
                                    <Image
                                      style={{margin: "30px 6px 0px 0px"}}
                                      alt="bot"
                                      src="./color.png"
                                      height={30}
                                      width={30}
                                    />
                                  </div>
                                  <div>
                                    <Text size={200}>
                                      {t.date}
                                    </Text>                               
                                      <div  style={{                                      
                                        margin: "4px 0px 0px 0px",                                                                                                                           
                                    }}>                                  
                                      <Card>
                                        <CardPreview>
                                          <p style={{ padding: '0px 18px 0px 18px'}}>
                                          <Markdown rehypePlugins={[[rehypeExternalLinks, {target: '_blank'}]]}
                                                  children={t.message}/>

                                          {/* { t.message ? 
                                            <div  dangerouslySetInnerHTML={{__html: this.formatMessageAsHtml(t.message)}}></div>
                                            :<div>Lo sentimos, pero ocurrió un error con el servicio (Respuesta no generada)</div>
                                          }                                         */}
                                      </p>

                                        {t?.canLike ? 

                                          <div style={{
                                            display:"grid",
                                            marginRight: "12px",
                                            marginBottom: "6px",                                        
                                            gridTemplateRows: "auto auto"}}>                                                                       
                                            <div style={{
                                                display:"grid",
                                                gridTemplateColumns: "auto 24px 24px"}}>                                                                                                             
                                                    <Text size={200} align="end" style={{paddingTop:"2px", marginRight: "4px"}}>
                                                        {t.tokens} tokens / {t.requestTime} segundos. 
                                                    </Text>
                                                    {this.DislikeButton(t, t.requestId)}  
                                                    {this.LikeButton(t)}                                                                                  
                                              </div>                                         
                                            </div>  
                                            : <></>
                                          }
                                      
                                        </CardPreview>
                                      </Card>
                                      <div>                                   
                                      </div>                                 
                                    
                                  </div>
                                </div>
                                </li>
                               ) : ( 
                                <li style={{
                                  display:"flex",
                                  overflow:"hidden",
                                  justifyContent:"flex-end", 
                                  alignItems:"flex-end"}}>                            
                                    <div>
                                        <div style={{
                                          textAlign:"end",                                     
                                          margin:"4px 24px 4px 0px"}}>
                                          <Text size={200}>{t.date}</Text>
                                        </div>              
                                        <div style={{
                                          padding: '6px 12px 6px 12px',
                                          margin: "0px 24px 0px 0px",
                                          borderRadius: "5px",
                                          backgroundColor: tokens.colorBrandForegroundInvertedHover
                                        }}>
                                        <Text color={tokens.colorBrandForegroundOnLight} size={300}>{t.message}</Text>
                                      </div>
                                    
                                    </div>
                                </li>
                               )}
                            </div>               
                          )
                        })
                      }   
                  </ul>                  
              )} 
          
              {/* Area de texto */}
              <div style={{margin: "0px 24px 24px 24px"}}>
              <Divider  style={{margin: '0px 12px 12px 0px'}}/>
              <div style={{
                    display: "grid",
                    gridTemplateColumns: "auto 32px 32px",
                    alignItems: "center",
                }}>                              
                  <Input value={this.state.prompt} onChange={this.onInputChange} 
                    style={{width: '100%'}} placeholder="¿Que deseas consultar?" 
                    contentAfter={<this.SearchButton/>} 
                    onKeyDown={(e) => { if (e.key === 'Enter') { this.queryReports(this.state?.prompt) } }}
                    disabled={this.state?.loading} maxLength={250}/>     
                    <div>
                      {<this.NewChatButton/>}                         
                    </div>
                    <div>
                      {<this.DrawerButton/>}
                    </div>        
                </div>  
                <Text style={{
                    paddingTop: "4px",
                    marginLeft: "2px",
                  }} size={200}>
                  Los resultados generados por AI pueden no ser exactos.   
                </Text>  
              </div>
          </div>
  
          {/* Side panel with the request history*/}       
          <Drawer
            open={this.state.isDrawerOpen}
            position="end"
            onOpenChange={() => this.setState({ isDrawerOpen: false })}
            style={{ backgroundColor: tokens.colorNeutralStencil1 }}
            
          >
            <DrawerHeader style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', position: 'relative' }}>
              <Button
                appearance="transparent"
                icon={<Dismiss16Regular />}
                onClick={() => this.setState({ isDrawerOpen: false })}
                style={{ position: 'absolute', right: 0 }}
              />
              <span style={{ fontWeight: 'bold' }}>Historial de Consultas</span>
            </DrawerHeader>
            <DrawerBody>
              <div>
                  <ChatHistory elements={this.state.chatHistory} onChatSelected={
                    (history) => {
                          this.setState({ currentChat: [], currentConversation: history.conversationId, isDrawerOpen: false }, ()=>
                          {
                            history.history.forEach( query => {                        
                              this.addMessageToChat(
                                query.requestId, 'user', query.prompt, false, false, 
                                0, 0, query.backendVersion, [], LikeState.None, moment(query.requestDate));                                
                              this.addMessageToChat(
                                query.requestId, 'bot', query.response,  true, false, 
                                query.tokens, query.requestTime, query.backendVersion, query.suggestedQuestions, 
                                query.liked, moment(query.requestDate));   
                            });
                            this.reloadChatHistory();
                          }); 
                    }
                  }/>
              </div> 
            </DrawerBody> 
          </Drawer>   
        </div>                                         
        :
        <>
        <LoginButton msalContext={this.props.msalContext} />
        <Text>
            {this.state.lastError}
        </Text>
        </>
    );
  };
}


export default withAITracking(reactPlugin, withMsal(Tab));