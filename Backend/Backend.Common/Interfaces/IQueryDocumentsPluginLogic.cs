using System.Threading.Tasks;

namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Documents plugin logic
    /// </summary>
    public interface IQueryDocumentsPluginLogic
    {

        /// <summary>
        /// Query a document
        /// </summary>    
        Task<string> QueryDocumentsAsync(string question);
    }
}
