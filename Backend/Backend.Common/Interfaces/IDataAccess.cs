using Backend.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Data Access interface
    /// </summary>
    public interface IDataAccess
    {
        /// <summary>
        /// Users Collection
        /// </summary>
        IRepository<User> Users { get; }


        /// <summary>
        /// Conversations Collection
        /// </summary>
        IRepository<Conversation> Conversations { get; }

        /// <summary>
        /// RequestsHistory Collection
        /// </summary>
        IRepository<RequestHistory> RequestsHistory { get; }

        /// <summary>
        /// StartUpOptions Collection
        /// </summary>
        IRepository<StartUpOption> StartUpOptions { get; }

        /// <summary>
        /// Current library structure
        /// </summary>
        IRepository<LibraryStructure> LibraryStructure { get; }

        /// <summary>
        /// Collection of AI plugins
        /// </summary>
        IRepository<AiPlugin> AiPlugins { get; }
      
        /// <summary>
        /// Saves all the changess
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Get sas token
        /// </summary>
        string GetSasToken(string container, int expiresOnMinutes);


        /// <summary>
        /// Add a message to a Queue
        /// </summary>
        Task<bool> AddMessageToQueueAsync(string queueName, string message);

        /// <summary>
        /// Delete a queue
        /// </summary>
        Task<bool> DeleteQueueAsync(string queueName);


        /// <summary>
        /// Saves a json file on a container
        /// </summary>    
        /// <returns></returns>
        Task<bool> SaveJsonFileAsyncAsync(string container, string folder, string fileName, string content);


        /// <summary>
        /// Returna a json file from a container
        /// </summary>
        Task<string> GetJsonFileAsync(string container, string folder, string fileName);
    }
}