using System;
using System.Collections.Generic;

namespace Backend.Models
{
    /// <summary>
    /// Like requests
    /// </summary>
    public class LikeRequest
    {
        public Guid ConversationId { get; set; }

        public Guid RequestId { get; set; }

        public bool Liked { get; set; }

        public string Comment { get; set; }
    }
}

