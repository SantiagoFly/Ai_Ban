using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Backend.Common.Extensions;
using System.Reflection;
using System.IO;

namespace Backend.AI.Services.Functions
{
    /// <summary>
    /// AI Plugin
    /// </summary>
    public class Plugin
    {
        private readonly string pluginPath = "/Manifest/ai-plugin.json";

        /// <summary>
        /// Returns the AI plugin description in json format
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Function("GetAIPluginJson")]
        public async Task<HttpResponseData> GetAIPluginJson([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/ai-plugin.json")] HttpRequestData request)
        {
            var currentDomain = $"{request.Url.Scheme}://{request.Url.Host}:{request.Url.Port}";
            var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var result = File.ReadAllText(binDirectory + this.pluginPath);
            var json = result.Replace("{url}", currentDomain);
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(json);
            return response;
        }
    }
}


