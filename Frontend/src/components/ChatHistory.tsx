

import { Card, CardHeader, Text, makeStyles, shorthands , tokens} from "@fluentui/react-components"
import { ConversationResultModel } from "../models/ConversationResultModel";
import Markdown from "react-markdown";
import moment from "moment";
import { QueryResultModel } from "../models/QueryResultModel";



interface ChatHistoryProps{
    elements: ConversationResultModel[]
    onChatSelected: (chat: ConversationResultModel) => void
}

interface ChatHistoryElementProps{
    chat: ConversationResultModel,
    onClick?: () => void
}

const useStyles = makeStyles({
    card: {
        width: "100%",
        marginTop: "8px",
        height: "fit-content",
        cursor: "pointer",
        ...shorthands.borderRadius("15px"),
    },
    chatHistoryContainer: {                   
        height: "100vh",
        overflowY: "scroll",
        ...shorthands.gap("4px"),  
    },
    cardDescriptionFooter: {
        marginTop: ".5em",
        fontSize: ".7em",
        textAlign: "right",
    },
    text: {
        ...shorthands.overflow("hidden"),
        width: "200px",
        display: "block",
    },
    textDescription: {
        ...shorthands.overflow("hidden"),
        width: "200px", 
        height: "14px",
        display: "block",       
    },
   
});

const ChatHistory = (props: ChatHistoryProps) => {
    const styles = useStyles();
    
    return (
        <div className={styles.chatHistoryContainer}>
            <div style={{textAlign: "center"}}>
                <Text weight="bold"></Text>
            </div>
            {props.elements.map( (conversation) => 
                <ChatHistoryElement 
                    key={conversation.conversationId}
                    chat={conversation}
                    onClick={ () => props.onChatSelected(conversation)}
                />
            )}
        </div>
    )
}


const ChatHistoryElement = (props: ChatHistoryElementProps) => {
    const styles =  useStyles();
    const date = moment(props.chat.date);
    const timeValue = date.date() === moment().date() ? date.format('HH:mm') : date.format('DD/MM/YYYY HH:mm');
    if (!props.chat.history || props.chat.history.length === 0) {
        return null; 
    }
    var chartHistoryItem = props.chat.history[0]; 
    return (
        <Card className={styles.card}onClick={props.onClick}>
            <CardHeader
                header={<div className="chat-history-title">{chartHistoryItem.prompt}</div>}
                description={
                    <div>
                        <div className="chat-history-content">
                            <Markdown children={chartHistoryItem.response}/>                       
                        </div>
                        <div className={styles.cardDescriptionFooter}>{timeValue}</div>
                    </div>
                }
            />
        </Card>
    )
}

export default ChatHistory;