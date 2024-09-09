using System;

namespace Backend.Models
{
    /// <summary>
    /// Available AI plugins
    /// </summary>
    public class AiPlugin
    {
        public Guid Id { get; set; }

        public string Uri { get; set; }

        public string Description { get; set; }

        public string Name { get; set;  }

        public string AccessToken { get; set; }
    }
}
