// Valid like states
export enum LikeState  {
    None = 0,
    Like = 1,
    Dislike = -1
  }

// A chat message
export interface ChatMessageModel {
    message: string;
    requestId: string;
    role: string;
    date: string;
    likeState: LikeState;
    canLike: boolean;
    backendVersion: string;
    tokens: number;
    requestTime: number,
    suggestedQuestions: string[];
}
  
  