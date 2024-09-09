using System;
using System.Collections.Generic;

namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Session provider to capture session information and make 
    /// it available to all classes by DI
    /// </summary>
    public interface ISessionProvider
    {
        /// <summary>
        /// Current user unique id
        /// </summary>
        Guid UserId { get; }


        /// <summary>
        /// Current user email
        /// </summary>
        string UserEmail { get;  }
     
        /// <summary>
        /// Validates the headers and extract the user information
        /// </summary>
        string ValidateHeaders(Dictionary<string, string> requestHeaders);
    }
}
