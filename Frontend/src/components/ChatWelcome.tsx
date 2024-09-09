import { Card, CardHeader, Text, makeStyles, shorthands } from "@fluentui/react-components";
import { StartUpOptionModel } from "../models/StartUpOptionModel";

interface WelcomeActionProps{
    title: string
    imageUrl: string
    description: string
    onClick?: () => void
}

export interface WelcomeAction{
    imageUrl: string
    title: string
    description: string
    color?: string
}
interface ChatWelcomePros{
    actions: StartUpOptionModel[]
    onActionSelected: (action: StartUpOptionModel) => void
}

const useStyles = makeStyles({
    welcomeContainer: {
        overflowY:"scroll",
        scrollbarWidth:"none",                 
        scrollBehavior:"smooth",
    },
    titleSection: {
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        ...shorthands.gap(".5em"),
    },
    titleSectionImage: {
        ...shorthands.borderRadius("50%"),
        width: "150px",
    },
    titleSectionTitle: {
        fontWeight: "bold",
        fontSize: "1.5em",
    },
    titleSectionSubTitle: {
        textAlign: "center",
        lineHeight: "1em",
    },
    titleSectionDivider: {
        width: "50%",
    },
    card: {
        width: "400px",
        maxWidth: "100%",
        cursor: "pointer",
        ...shorthands.borderRadius("15px"),
    },
    cardImage: {
        width: "50px",
        aspectRatio: "1/1",
        ...shorthands.borderRadius("50%"),
    },

    actionsList: {
        display: "flex",
        ...shorthands.gap("30px"),
        flexWrap: "wrap",
        justifyContent: "center",
        alignItems: "stretch"
    },

    action: {
        cursor: "pointer",
    },
    actionListDescription: {
        textAlign: "center",
    }
});

const ChatWelcome = (props: ChatWelcomePros) => {
    const styles =  useStyles();

    return (
        <div className={styles.welcomeContainer}>
            <div className={styles.titleSection}>
                <img className={styles.titleSectionImage} src="./Bancard-Logo.png" alt="Logo Bancard" />
                <hr />
                <h1 className={styles.titleSectionSubTitle}>HOLA!</h1>
            </div>
            <div>
                <p className={styles.actionListDescription}>Algunos ejemplos de las consultas que puedes realizar:</p>
                <div className={styles.actionsList}>
                    {props.actions.map((action) => {
                        return <ChatWelcomeAction
                                key={action.text}
                                title={action.text} 
                                imageUrl=""  
                                description=""                 
                                onClick={ () => props.onActionSelected(action)}
                    />
                    })}
                </div>
            </div>
        </div>
    )
}


const ChatWelcomeAction = (props: WelcomeActionProps) => {
    const styles =  useStyles();
    
    
    return (
        <Card
        className={styles.card}
        onClick={props.onClick}
        >
        <CardHeader
            image={
                props.imageUrl ? <img className={styles.cardImage} src={props.imageUrl} alt="Action" /> : null
            }
            header={<Text weight="semibold">{props.title}</Text>}
            description={
                <p className="chat-welcome-action-description">{props.description}</p>
            }
        />
        </Card>
    )
    
}

export default ChatWelcome