using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace Backend.Common.Middleware
{
    /// <summary>
    /// Middleware to validate a custom bearer token
    /// </summary>
    public class BearerTokenMiddleware : IFunctionsWorkerMiddleware
    {
        /// <summary>
        /// Called when the middleware is used.
        /// The scoped service is injected into Invoke
        /// </summary>
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            try
            {
                var apiKey = "AODaJq1MQTB_48vjZBftvBYiZRtrwHY6e8pJgL-gvRPuAzFumchssw==";
                var httpRequqestData = await context.GetHttpRequestDataAsync();
                if (httpRequqestData != null)
                {
                    if (!httpRequqestData.Url.ToString().Contains("api/swagger", StringComparison.InvariantCultureIgnoreCase)
                        && !httpRequqestData.Url.ToString().Contains("api/.well-known/ai-plugin.json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (string.Equals(GetBearerToken(context), apiKey, StringComparison.InvariantCultureIgnoreCase))
                        {
                            await next(context);
                            return;
                        }                     
                    }
                    else
                    {
                        await next(context);
                        return;
                    }
                }
                var newHttpResponse = httpRequqestData.CreateResponse(HttpStatusCode.Unauthorized);
                context.GetInvocationResult().Value = newHttpResponse;                   
            }
            catch (Exception ex)
            {              
                var httpRequqestData = await context.GetHttpRequestDataAsync();
                var newHttpResponse = httpRequqestData.CreateResponse(HttpStatusCode.BadRequest);
                await newHttpResponse.WriteAsJsonAsync(new { ResponseStatus = $"Middleware error { ex.Message}" }, newHttpResponse.StatusCode);
                context.GetInvocationResult().Value = newHttpResponse;
            }
        }


        /// <summary>
        /// Gets a bearer token from the request headers
        /// </summary>
        private static string GetBearerToken(FunctionContext context)
        {
            if (context.BindingContext.BindingData is IReadOnlyDictionary<string, object> bindingData && bindingData.ContainsKey("headers"))
            {
                var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(bindingData["headers"].ToString());
                if (headers.TryGetValue("Authorization", out string authorizationHeader))
                {
                    var values = authorizationHeader.Split(" ");
                    if (values.Length == 2)
                    {
                        return authorizationHeader.Split(" ")[1];                       
                    }
                }
            }
            return string.Empty;         
        }
    }
}
