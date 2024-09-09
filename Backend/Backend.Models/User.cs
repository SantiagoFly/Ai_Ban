using System;

namespace Backend.Models
{
    /// <summary>
    /// Users details to control the number of 
    /// queries made per user per day
    /// </summary>
    public class User 
    {
        public Guid UserId { get; set; }

        public string Email { get; set; }

        public DateTimeOffset LastQueryDate { get; set; }

        public int QueryCount { get; set; }

        public int? TokensCount { get; set; }

    }
}
