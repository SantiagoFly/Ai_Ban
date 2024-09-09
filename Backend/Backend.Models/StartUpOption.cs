
using System;

namespace Backend.Models
{
    /// <summary>
    /// Options to show to the user on startup
    /// </summary>
    public class StartUpOption
    {
        public Guid StartUpOptionId { get; set; }

        public string Title { get; set; }

        public string Text { get; set; }

        public string Color { get; set; }

        public string Icon { get; set; }
   
    }
}
