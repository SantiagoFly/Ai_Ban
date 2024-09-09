import { QueryResultModel } from "./QueryResultModel"

export interface ConversationResultModel {
    conversationId: string
    userId: string
    email: string
    prompt: string
    response: string
    date: string
    history: QueryResultModel[]
}