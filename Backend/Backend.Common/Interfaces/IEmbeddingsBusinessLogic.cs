using Backend.Common.Models;
using Backend.Models;
using System.Threading.Tasks;

namespace Backend.Common.Interfaces
{
    /// <summary>
    /// Embeddings creation business logic
    /// </summary>
    public interface IEmbeddingsBusinessLogic
    {      
        /// <summary>
        /// Creates the embedding a for a document in a folder
        /// </summary>
        Task<string> CreateEmbeddingsAsync(EmbeddingRequest request);


        /// <summary>
        /// Creates the embedding a for a document in a folder
        /// </summary>
        Task<Result<string>> CreateEmbeddingsForFileAsync(EmbeddingRequest request);


        /// <summary>
        /// Checks for elements to process the embedding.
        /// </summary>
        /// <returns></returns>
        Task<bool> CheckDocumentsForEmbeddingQueueAsync();
    }
}