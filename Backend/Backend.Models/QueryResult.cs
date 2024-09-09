using System;
using System.Collections.Generic;

namespace Backend.Models
{
    /// <summary>
    /// Search result
    /// </summary>
    public class QueryResult 
    {
        public string Prompt { get; set; }

        public string Summary { get; set; }

        public int Tokens { get; set; }

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public DateTimeOffset RequestDate { get; set; }

        public Guid RequestId { get; set; }

        public List<string> SuggestedQuestions { get; set; }

        public int PlannerTokens { get; set; }
        
        public double RequestTime { get; set; }

        public Guid ConversationId { get; set; }

        public string SuggestedPrompt { get; set; }

        public (List<DocumentDetails>, string) AdditionalInformation { get; set; }
    }
    
}

