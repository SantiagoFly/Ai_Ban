using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using Backend.Common.Interfaces;

namespace Backend.Embedding.Functions
{
    /// <summary>
    /// Function to execute the sync process for the sharepoint documents
    /// </summary>
    /// <remarks>
    /// Receive all the depedencies by DI
    /// </remarks>        
    public class SharePointSync(ISharePointSyncLogic businessLogic)
    {
        private readonly ISharePointSyncLogic businessLogic = businessLogic;


#if !DEBUG
        /// <summary>
        /// Starts the sync process for the sharepoint documents
        /// </summary>
        [Function(nameof(SyncSharepointDocumentsAsync))]
        public async Task SyncSharepointDocumentsAsync([TimerTrigger("*/30 * * * *")] TimerInfo timerInfo, FunctionContext context)
        {
            await this.businessLogic.SyncSharepointDocumentsAsync();
        }

#endif

        /// <summary>
        /// Simple endpoing to start the process on demands
        /// </summary>
        [OpenApiOperation("Start", ["Documents"], Description = "Start proccess")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(HttpStatusCode), Description = "Result the proccess")]
        [Function(nameof(StartSyncSharepointDocumentsAsync))]
        public async Task<HttpResponseData> StartSyncSharepointDocumentsAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync/start")] HttpRequestData request)
        {
            await this.businessLogic.SyncSharepointDocumentsAsync();
            return request.CreateResponse(HttpStatusCode.OK);
        }
    }

}


