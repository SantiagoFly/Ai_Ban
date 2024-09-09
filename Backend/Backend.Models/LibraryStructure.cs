using System;
using System.Collections.Generic;
using System.Text;

namespace Backend.Models
{
    /// <summary>
    /// Library structure
    /// </summary>
    public class LibraryStructure
    {

        public Guid Id { get; set; }

        public string SiteUri { get; set; }

        public string RootFolder { get; set; }

        public List<EmbeddingRequest> Documents { get; set; }

        public DateTime LastUpdate { get; set; }
    }
}
