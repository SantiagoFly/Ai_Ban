using System.Threading.Tasks;
using System.Collections.Generic;
using Backend.Common.Models;
using Backend.Models;

namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Request history business logic
    /// </summary>
    public interface IHistoryBusinessLogic
    {

        /// <summary>
        /// Returns the request history for last 30 days
        /// </summary>
        Task<Result<ICollection<RequestHistory>>> GetRequestHistoryAsync(int days);


        /// <summary>
        /// Returns the user conversation history
        /// </summary>
        Task<Result<ICollection<Conversation>>> GetUserChatHistoryAsync();


        /// <summary>
        /// Returns the last conversation from the current uset
        /// </summary>
        Task<Result<Conversation>> GetLastConversationAsync();
    }
}
