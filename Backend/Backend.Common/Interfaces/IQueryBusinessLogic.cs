using System.Threading.Tasks;
using Backend.Models;
using Backend.Common.Models;

namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Project business logic interface
    /// </summary>
    public interface IQueryBusinessLogic
    {

        /// <summary>
        /// Returns the startup options
        /// </summary>
        /// <returns></returns>
        Task<Result<StartUpOption[]>> GetStartUpOptionsAsync();


        /// <summary>
        /// Prompt the query
        /// </summary>
        Task<Result<QueryResult>> QueryAsync(QueryRequest request);


        /// <summary>
        /// Likes a query result
        /// </summary>
        Task<Result<bool>> LikeQueryResultAsync(LikeRequest request);


        /// <summary>
        /// Returns the suggested prompt
        /// </summary>
        Task<Result<QueryResult>> GetSuggestedPrompt(QueryRequest request);



    }
}
