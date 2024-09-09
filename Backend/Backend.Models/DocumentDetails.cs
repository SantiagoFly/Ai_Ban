using System.Collections.Generic;

namespace Backend.Models
{
    /// <summary>
    /// Document details
    /// </summary>
    public class DocumentDetails
    {
        public string Content { get; set; }
        public string Title { get; set; }
        public string DocumentUri { get; set; }
        public string File { get; set; }
        public string DocumentId { get; set; }

        public List<DocumentDetailCitation> Citations { get; set; }
    }

    public class DocumentDetailCitation
    {
        public string Content { get; set; }

        public double Score { get; set; }

        public double RerankScore { get; set; }
    }
}

