using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Backend.Common.Interfaces;
using Backend.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Backend.Common.Extensions;
using Newtonsoft.Json;

namespace Backend.Embedding.Functions
{
    /// <summary>
    /// Function to create the embeddings for a document
    /// </summary>
    /// <remarks>
    /// Receive all the depedencies by DI
    /// </remarks>        
    public class Embeddings(IEmbeddingsBusinessLogic businessLogic, ILogger<Embeddings> logger)
    {
        private readonly ILogger<Embeddings> logger = logger;
        private readonly IEmbeddingsBusinessLogic businessLogic = businessLogic;


        /// <summary>
        /// Start processing one element from the queue
        /// </summary>
        [Function(nameof(CreateEmbeddingsAsync))]
        public async Task CreateEmbeddingsAsync([QueueTrigger("library-to-process", Connection = "StorageConnectionString")] string queueItem)
        {
            try
            {
                var embeddingRequest = JsonConvert.DeserializeObject<EmbeddingRequest>(queueItem);
                await this.businessLogic.CreateEmbeddingsAsync(embeddingRequest);
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, $"Unable to deserialize queue item: {queueItem}");
            }
        }

#if !DEBUG
        /// <summary>
        /// Starts the sync process for the sharepoint documents
        /// </summary>
        [Function(nameof(CheckForEmbeddingsAsync))]
        public async Task CheckForEmbeddingsAsync([TimerTrigger("*/15 * * * *")] TimerInfo timerInfo, FunctionContext context)
        {
            await this.businessLogic.CheckDocumentsForEmbeddingQueueAsync();
        }

#endif
        /// <summary>
        /// Creates the embeddings for a specific file
        /// </summary>
        [OpenApiOperation("Checks for embedding", ["Embedding"], Description = "Creates the embeeding for a especific file")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(HttpStatusCode), Description = "Result the proccess")]
        [Function(nameof(CheckDocumentsForEmbeddingAsync))]
        public async Task<HttpResponseData> CheckDocumentsForEmbeddingAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "embeddings/check")] HttpRequestData request)
        {
            _ = await this.businessLogic.CheckDocumentsForEmbeddingQueueAsync();
            return request.CreateResponse(HttpStatusCode.OK);
        }


        /// <summary>
        /// Creates the embeddings for a specific file
        /// </summary>
        [OpenApiOperation("Create embedding", ["Embedding"], Description = "Creates the embeeding for a especific file")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(HttpStatusCode), Description = "Result the proccess")]
        [Function(nameof(ProcessEmbeddingsAsync))]
        public async Task<HttpResponseData> ProcessEmbeddingsAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "embeddings/process")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.CreateEmbeddingsForFileAsync, 
                request.DeserializeBody<EmbeddingRequest>(), responseLinks =>
            {
                responseLinks.Links = [];
            }, logger);         
        }

    }

}


