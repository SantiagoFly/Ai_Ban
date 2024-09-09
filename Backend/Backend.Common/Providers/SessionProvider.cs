using Backend.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;

namespace Backend.Common.Providers
{
    /// <inheritdoc/>  
    public class SessionProvider : ISessionProvider
    {

        /// <inheritdoc/>  
        public Guid UserId { get; private set; }


        /// <inheritdoc/>  
        public string UserEmail { get; private set; }


        /// <inheritdoc/>  
        public string ValidateHeaders(Dictionary<string, string> requestHeaders)
        {
            try
            {
                var userIdKey = requestHeaders.Keys.FirstOrDefault(x => string.Equals(x, "UserId", StringComparison.InvariantCultureIgnoreCase));
                var userEmailKey = requestHeaders.Keys.FirstOrDefault(x => string.Equals(x, "UserEmail", StringComparison.InvariantCultureIgnoreCase));
                if (!requestHeaders.TryGetValue(userIdKey, out string userId)) return "UserId header missing or invalid";
                if (!requestHeaders.TryGetValue(userEmailKey, out string userEmail)) return "UserEmail header missing or invalid";
                this.UserId = new Guid(userId.ToString());
                this.UserEmail = userEmail;
                return string.Empty;
            }
            catch (Exception ex)
            {
                // Error processing headers
                return "Invalid headers: " + ex.Message;
            }
        }
    }
}
