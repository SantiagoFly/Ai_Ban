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
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Backend.AI.Services.Functions
{
    /// <summary>
    /// Documents pugin
    /// </summary>
    /// <remarks>
    /// Receive all the depedencies by DI
    /// </remarks>        
    public class Documents(IQueryDocumentsPluginLogic businessLogic)
    {
        private readonly IQueryDocumentsPluginLogic businessLogic = businessLogic;


        /// <summary>
        /// QueryAsync
        /// </summary>       
        [OpenApiOperation("Query", ["Query"], Description = "Answer a question from the user by searching the company documents")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "text/plain; charset=utf-8", typeof(string), Description = "Answer to the question")]
        [OpenApiParameter("question", Required = true, Type = typeof(string), In = ParameterLocation.Query, Description = "Question made")]
        [Function(nameof(QueryAsync))]
        public async Task<HttpResponseData> QueryAsync(
         [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query")] HttpRequestData request, [FromQuery] string question)
        {
            var result = await this.businessLogic.QueryDocumentsAsync(question);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(result);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            return response;
        }
    }
}


