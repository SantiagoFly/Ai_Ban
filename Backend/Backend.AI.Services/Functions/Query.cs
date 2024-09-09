using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Backend.Models;
using Backend.Common.Models;
using Backend.Common.Extensions;
using System;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Backend.Common.Interfaces;

namespace Backend.AI.Services.Functions
{
    /// <summary>
    /// Projects API
    /// </summary>
    /// <remarks>
    /// Receive all the depedencies by DI
    /// </remarks>        
    public class Query(IQueryBusinessLogic businessLogic, ILogger<Query> logger)
    {
        private readonly ILogger<Query> logger = logger;
        private readonly IQueryBusinessLogic businessLogic = businessLogic;


        /// <summary>
        /// Search documents
        /// </summary>       
        [OpenApiOperation("Query", ["Documents"], Description = "Query the documents knowledge base")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(QueryRequest), Required = true, Description = "Prompt to request (The operationId is ignored)")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<QueryResult>), Description = "Query the documents knowledge base")]
        [Function(nameof(QueryKnowledgeBaseAsync))]
        public async Task<HttpResponseData> QueryKnowledgeBaseAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "query")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.QueryAsync, request.DeserializeBody<QueryRequest>(), responseLinks =>
            {
                responseLinks.Links = [];
            }, logger);
        }



        /// <summary>
        /// Likes a request 
        /// </summary>       
        [OpenApiOperation("Like", ["Documents"], Description = "Likes the result ")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LikeRequest), Required = true, Description = "Like request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<bool>), Description = "Likes a query result")]
        [Function(nameof(LikeQueryResultAsync))]
        public async Task<HttpResponseData> LikeQueryResultAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "query/like")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.LikeQueryResultAsync, request.DeserializeBody<LikeRequest>(), responseLinks =>
            {
                responseLinks.Links = [];
            }, logger);
        }


        /// <summary>
        /// Returns the startup options
        /// </summary>       
        [OpenApiOperation("Search", ["Documents"], Description = "Returns the startup options")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<StartUpOption>), Description = "Query the documents knowledge base")]
        [Function(nameof(GetStartUpOptionsAsync))]
        public async Task<HttpResponseData> GetStartUpOptionsAsync(
         [HttpTrigger(AuthorizationLevel.Function, "get", Route = "startup/options")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.GetStartUpOptionsAsync, responseLinks =>
            {
                responseLinks.Links = [];
            }, logger);
        }


        /// <summary>
        /// Returns the suggested prompt
        /// </summary>       
        [OpenApiOperation("Query", ["Documents"], Description = "Returns the suggested prompt")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "x-functions-key", In = OpenApiSecurityLocationType.Header, Description = "The function key to access the API")]
        [OpenApiParameter("userId", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User id who makes the request")]
        [OpenApiParameter("userEmail", In = ParameterLocation.Header, Required = true, Type = typeof(string), Description = "User emaill who makes the request")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(Response<StartUpOption>), Description = "Query the documents knowledge base")]
        [Function(nameof(GetSuggestedPromptAsync))]
        public async Task<HttpResponseData> GetSuggestedPromptAsync(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "query/suggested")] HttpRequestData request)
        {
            return await request.CreateResponse(this.businessLogic.GetSuggestedPrompt, request.DeserializeBody<QueryRequest>(), responseLinks =>
            {
                responseLinks.Links = [];
            }, logger);
        }

        
    }
}


