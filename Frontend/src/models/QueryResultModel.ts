import { LikeState } from "./ChatMessageModel";


// A query result from the API
export interface QueryResultModel {
    requestId: string;
    userId: string;
    email: string;
    prompt: string;
    liked: LikeState;
    summary: string;
    response: string;
    requestDate: string;
    tokens: number;
    comments: string;
    suggestedQuestions: string[];
    suggestedPrompt: string;
    backendVersion: string;
    promptTokens: number;
    completionTokens: number;
    plannerTokens: number;
    requestTime: number;
    conversationId: string;
}