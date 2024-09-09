using System;
using System.Collections.Generic;
using System.Text;

namespace Backend.Models
{
    /// <summary>
    /// Request history
    /// </summary>
    public class RequestHistory
    {
        public Guid RequestId { get; set; }

        public Guid? ConversationId { get; set; }

        public Guid UserId { get; set; }

        public string Email { get; set; }

        public string Prompt { get; set; }

        /// <summary>
        /// 1 = liked, -1 = disliked, 0 = unknown
        /// </summary>
        public int Liked { get; set; }

        public string Response { get; set; }

        public DateTimeOffset RequestDate { get; set; }

        public int Tokens { get; set; }
        public string Comments { get; set; }
   
        public List<string> SuggestedQuestions { get; set; }

        public int? PromptTokens { get; set; } = 0;
    
        public int? CompletionTokens { get; set; } = 0;

        public int? PlannerTokens { get; set; } = 0;

        public double? RequestTime { get; set; }

        public string SuggestedPrompt { get; set; }

        public ICollection<DocumentDetails> Documents { get; set; }
    }
}
