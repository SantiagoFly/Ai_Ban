using System;

namespace Backend.Models
{
    public class EmbeddingRequest
    {
        public string Folder { get; set; }
     
        public string? Filename { get; set; }

        public string? SiteUri { get; set; }

        public string? Uri { get; set; }
        
        public string? Version { get; set; }
        
        public DateTime TimeLastModified { get; set; }

        public bool IsFolder { get; set; }
        
        public string?[]? GroupIds { get; set; }
        
        public string? Hash { get; set; }
        
        public string Year { get; set; }

        public bool ToBeUpdated { get; set; }

        public bool ToBeDeleted { get; set; }
    }
}
