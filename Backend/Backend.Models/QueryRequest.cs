using System;

namespace Backend.Models
{
    /// <summary>
    /// Search request
    /// </summary>
    public class QueryRequest
    {       
        public string Prompt { get; set; }

         public Guid? ConversationId { get; set; }
    }
}

