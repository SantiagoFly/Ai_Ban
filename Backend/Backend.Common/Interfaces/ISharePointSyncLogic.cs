using System.Threading.Tasks;

namespace Backend.Common.Interfaces
{ 
    /// <summary>
    /// Logic for the sync process for the sharepoint documents
    /// </summary>
    public interface ISharePointSyncLogic
    {
        /// <summary>
        /// Starts the sync process 
        /// </summary>
        Task SyncSharepointDocumentsAsync();

        /// <summary>
        /// Clean Sync and starts the sync process 
        /// </summary>
        Task CleanSyncSharepointDocumentsAsync();
    }
}
