using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Models;
using Backend.Common.Logic;
using Microsoft.VisualBasic;

namespace Backend.Service.BusinessLogic
{
    /// <summary>
    /// Gets by DI the dependeciees
    /// </summary>
    public class HistoryBusinessLogic(ISessionProvider sessionProvider, IDataAccess dataAccess, ILogger<HistoryBusinessLogic> logger) : BaseLogic(sessionProvider, dataAccess, logger), IHistoryBusinessLogic
    {


        /// <inheritdoc/>
        public async Task<Result<ICollection<RequestHistory>>> GetRequestHistoryAsync(int days)
        {
            try
            {
                this.logger?.LogInformation("Executing HistoryBusinessLogic.GetRequestHistoryAsync");
                var result = await this.dataAccess.RequestsHistory.GetAsync();           
                return new Result<ICollection<RequestHistory>>(result.OrderByDescending(x => x.RequestDate).ToList());
            }
            catch (Exception ex)
            {
                return Error<ICollection<RequestHistory>>(ex, null);             
            }
        }


        /// <inheritdoc/>
        public async Task<Result<ICollection<Conversation>>> GetUserChatHistoryAsync()
        {
            try
            {
                this.logger?.LogInformation("Executing HistoryBusinessLogic.GetUserLatestsRequestsAsync");
                var result = await this.dataAccess.Conversations.GetAsync(x => x.UserId == this.sessionProvider.UserId, null, string.Empty);
                return new Result<ICollection<Conversation>>(result.OrderByDescending(x => x.Date).Take(10).ToList());
            }
            catch (Exception ex)
            {
                return Error<ICollection<Conversation>>(ex, null);   
            }
        }


        /// <inheritdoc/>
        public async Task<Result<Conversation>> GetLastConversationAsync()
        {
            try
            {
                var result = await this.dataAccess.Conversations.GetAsync(x => x.UserId == this.sessionProvider.UserId, null, string.Empty);
                var lastConversation = result.OrderByDescending(x => x.Date).FirstOrDefault();
                return new Result<Conversation>(lastConversation);
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
                return new Result<Conversation>(ex.Message);
            }
        }
    }
}
