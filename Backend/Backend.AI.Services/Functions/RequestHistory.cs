using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Backend.Common.Interfaces;
using Azure;
using Backend.Common.Extensions;
using Backend.Models;
using System.Text;
using Microsoft.Identity.Client;

namespace Backend.AI.Services.Functions
{
    /// <summary>
    /// Projects API
    /// </summary>
    /// <remarks>
    /// Receive all the depedencies by DI
    /// </remarks>        
    public class RequestHistory(IHistoryBusinessLogic businessLogic, ILogger<RequestHistory> logger)
    {
        private readonly ILogger<RequestHistory> logger = logger;
        private readonly IHistoryBusinessLogic businessLogic = businessLogic;


        /// <summary>
        /// Returns the request history
        /// </summary>       
        [OpenApiOperation("GetRequestHistoryAsync", ["History"], Description = "Returns the request history")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<RequestHistory[]>), Description = "Query the documents knowledge base")]
        [Function(nameof(GetRequestHistoryAsync))]
        public async Task<HttpResponseData> GetRequestHistoryAsync(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "requests/history")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.GetRequestHistoryAsync, 30, responseLinks =>{ responseLinks.Links = []; }, logger);
        }



        /// <summary>
        /// Returns the request Latests
        /// </summary>       
        [OpenApiOperation("GetRequestLatestsAsync", ["History"], Description = "Returns the request latests")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<Conversation[]>), Description = "Query the documents knowledge base")]
        [Function(nameof(GetUserLatestsRequestsAsync))]
        public async Task<HttpResponseData> GetUserLatestsRequestsAsync(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "chat/history")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.GetUserChatHistoryAsync, responseLinks => { responseLinks.Links = []; }, logger);
        }


        /// <summary>
        /// Returns the request Latests
        /// </summary>       
        [OpenApiOperation("GetLastConversationAsync", ["History"], Description = "Returns the request latests")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<Conversation[]>), Description = "Query the documents knowledge base")]
        [Function(nameof(GetLastConversationAsync))]
        public async Task<HttpResponseData> GetLastConversationAsync(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "chat/last")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.GetLastConversationAsync, responseLinks => { responseLinks.Links = []; }, logger);
        }


        /// <summary>
        /// Returns the data in a csv format
        /// </summary>       
        [OpenApiOperation("ExportChatHistoryAsync", ["History"], Description = "Returns the request latests")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<Conversation[]>), Description = "Query the documents knowledge base")]
        [Function(nameof(ExportChatHistoryAsync))]
        public async Task<HttpResponseData> ExportChatHistoryAsync(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "requests/history/export")] HttpRequestData request)
        {
            var data = await this.businessLogic.GetRequestHistoryAsync(90);
            var csv = new StringBuilder();
            csv.AppendLine("Date,Email,Prompt,Response,Liked,Comments,RequestTime,PromptTokens,CompletionTokens,ConversationId,RequestId");

            foreach (var item in data.Data)
            {
                //var prompt = MakeCsvSafe(item.Prompt);
                //var answer = item.Response.Contains(",") ? $"\"{item.Response.Replace("\"", "\"\"")}\"" : item.Response;
                //var comments = item.Comments.Contains(",") ? $"\"{item.Comments.Replace("\"", "\"\"")}\"" : item.Comments;
                csv.AppendLine($"{item.RequestDate.ToString("dd-MM-yyyy HH:mmm")}," +
                    $"{item.Email}," +
                    $"{MakeCsvSafe(item.Prompt)}," +
                    $"{MakeCsvSafe(item.Response)}," +
                    $"{item.Liked}," +
                    $"{MakeCsvSafe(item.Comments)}," +
                    $"{item.RequestTime}," +
                    $"{item.PromptTokens}," +
                    $"{item.CompletionTokens}," +
                    $"{item.ConversationId}," +
                    $"{item.RequestId}");
            }
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/csv");
            response.Headers.Add("Content-Disposition", "attachment; filename=data.csv");
            await response.WriteStringAsync(csv.ToString());
            return response;
        }


        private static string MakeCsvSafe(string input)
        {
            // Replace double quotes with two double quotes
            var escapedInput = input.Replace("\"", "\"\"");

            // Replace line breaks with a space (or another suitable placeholder)
            escapedInput = escapedInput.Replace("\r", " ").Replace("\n", " ");

            // Encapsulate in double quotes
            return $"\"{escapedInput}\"";
        }
    }
}


