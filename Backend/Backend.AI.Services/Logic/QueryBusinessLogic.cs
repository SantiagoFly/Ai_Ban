#pragma warning disable SKEXP0061
#pragma warning disable SKEXP0060
#pragma warning disable SKEXP0004
#pragma warning disable SKEXP0040
#pragma warning disable CS0618 

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Extensions.Configuration;
using Azure.Core;
using System.Collections.Generic;
using System.Linq;
using Backend.Common.Logic;
using Backend.Common.Interfaces;
using Backend.Models;
using Backend.Common.Models;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel;
using Azure.AI.OpenAI;
using Azure;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;

namespace DocumentSearch.BusinessLogic
{
    /// </inheritdoc/>
    public class QueryBusinessLogic : BaseLogic, IQueryBusinessLogic
    {
        private readonly double topP = 0.1;
        private readonly string azureOpenAIApiKey;
        private readonly string azureOpenAIApiEndpoint;
        private readonly string azureOpenAIChatCompletionModel;
        private readonly int maxQueriesPerDay;
        private readonly int maxTokensPerDay;     
        private readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Gets by DI the dependeciees
        /// </summary>
        /// <param name="dataAccess"></param>
        public QueryBusinessLogic(ISessionProvider sessionProvider,
            IDataAccess dataAccess, IConfiguration configuration,  ILogger<QueryBusinessLogic> logger, ILoggerFactory loggerFactory) : base(sessionProvider, dataAccess, logger)
        {
            this.loggerFactory = loggerFactory;
            this.azureOpenAIApiKey = configuration["AzureOpenAIApiKey"];
            this.azureOpenAIApiEndpoint = configuration["AzureOpenAIApiEndpoint"];
            this.azureOpenAIChatCompletionModel = configuration["AzureOpenAIChatCompletionModel"];       
            this.maxQueriesPerDay = int.Parse(configuration["MaxQueriesPerDay"].ToString());
            this.maxTokensPerDay = int.Parse(configuration["MaxTokensPerDay"].ToString());
        }


        /// <summary>
        /// Returns the current user info
        /// </summary>
        private async Task<User>  GetUserAsync()
        {
            var user = await this.dataAccess.Users.GetAsync(this.sessionProvider.UserId);
            if (user == null)
            {
                user = new User
                {
                    UserId = this.sessionProvider.UserId,
                    Email = this.sessionProvider.UserEmail,
                    QueryCount = 1,
                    TokensCount =  0,
                    LastQueryDate = DateTime.UtcNow,
                };
                await this.dataAccess.Users.InsertAsync(user);
                await this.dataAccess.SaveChangesAsync();
            }
            return user;
        }


        /// <summary>
        /// Validates the user quota
        /// </summary>
        private bool CheckUserQuota(User user)
        {

            this.logger?.LogInformation("Executing DocumentBusinessLogic.CheckUserQuota");
            if (user.LastQueryDate.Day == DateTime.UtcNow.Day
                && user.LastQueryDate.Month == DateTime.UtcNow.Month
                && user.LastQueryDate.Year == DateTime.UtcNow.Year)
            {
                user.TokensCount ??= 0;
                if (user.TokensCount >= this.maxTokensPerDay)
                {
                    return false;
                }
                else
                {
                    user.QueryCount++;
                    user.LastQueryDate = DateTime.UtcNow;
                }
            }
            else
            {
                // Reset the counter
                user.TokensCount = 0;
                user.QueryCount = 1;
                user.LastQueryDate = DateTime.UtcNow;
            }
            return true;
        }


        /// <inheritdoc/>
        public async Task<Result<StartUpOption[]>> GetStartUpOptionsAsync()
        {
            try
            {   
                
                var startUpOptions = await this.dataAccess.StartUpOptions.GetAsync();
                var result = new Result<StartUpOption[]>(startUpOptions.ToArray());
                return result;
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
                return new Result<StartUpOption[]>(ex.Message);
            }
        }



        /// <inheritdoc/>
        public async Task<Result<QueryResult>> QueryAsync(QueryRequest request)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();
                var result = new QueryResult();
                var startTime = DateTime.Now;
                this.logger?.LogInformation("Executing DocumentBusinessLogic.QueryReportsAsync");

                //// Check the quota
                this.logger?.LogInformation($"Checking {this.sessionProvider?.UserEmail} information and current quota");
                var user = await GetUserAsync();
                var hasQuota = CheckUserQuota(user);

                if (!hasQuota)
                {
                    this.logger?.LogInformation($"{this.sessionProvider?.UserEmail} cannot make more request for today");
                    return new Result<QueryResult>(new QueryResult
                    {
                        Prompt = request.Prompt,
                        Summary = $"Ya realizó el máximo de {this.maxQueriesPerDay} consultas por día. Puede intentar mañana nuevamente",
                    });
                }

                // Query the Azure Open AI
                var conversation = await GetConversationAsync(request);
              
                // Executes the planner
                var kernel = await SetupSemanticKernelAsync(true);
                if (kernel == null)
                {
                    return new Result<QueryResult>(new QueryResult
                    {
                        Prompt = request.Prompt,
                        Summary = "No se pudo inicializar el kernel de OpenAI",
                    });
                }

                this.logger?.LogInformation($"Querying about '{request.Prompt}'");

                // Infer the user question based on th conversation history                
                var suggestedPrompt = CleanUpText(request.Prompt);
                var chatSystemPrompt = $"""
                    Eres un asistente de AI para la búsqueda y consulta de documentos, procedimientos y manuales asi como otros documentos.
                    Tu misión es brindar ayuda y entregar información clara referente a los que existen, debes responder en español en tono formal.
                    Responder la siguiente consulta del usuario: '{suggestedPrompt}'.
                    No consultar internet para información adicional.
                    No intepretar la respuesta generada por las funciones que dispones.
                    Si no tienes una respuesta responde amablemente que no tienes información disponible para responder y sugiere reformurlar la pregunta.
                    Mantener el formato de respuesta en markdown.                    
                   """;

                var planResult = await kernel.InvokePromptAsync(chatSystemPrompt, new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 1000,
                    TopP = this.topP,                    
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                }));

                var promptTokens = 0;
                var completionTokens = 0;
                var totalTokens = 0;
                if (planResult.Metadata.ContainsKey("Usage"))
                {
                    var usage = (CompletionsUsage)planResult.Metadata["Usage"];
                    promptTokens = usage.PromptTokens;
                    completionTokens = usage.CompletionTokens;
                    totalTokens = promptTokens + completionTokens;
                }

                var finalAnswer = planResult.GetValue<string>();
                this.logger?.LogInformation($"Answer: {finalAnswer}");

                //create the chat history
                //var kernelPrompt = $"""
                //    Escribir tres preguntas relacionadas con el siguiente texto: "{finalAnswer}"                                 
                //   """;
                //var suggestedQuestionsResult = await kernel.InvokePromptAsync(kernelPrompt);
                //var suggestedQuestionsList = suggestedQuestionsResult.GetValue<string>();
                //var suggestedQuestions = suggestedQuestionsList.Split("\n").ToList();

                stopWatch.Stop();
                result = new QueryResult
                {
                    RequestId = Guid.NewGuid(),
                    ConversationId = conversation.ConversationId,
                    Prompt = request.Prompt,
                    RequestDate = DateTime.UtcNow,
                    RequestTime = Math.Round(stopWatch.ElapsedMilliseconds  / 1000.0, 2),
                    SuggestedPrompt = suggestedPrompt,
                    Summary = finalAnswer,
                    Tokens = totalTokens,
                    PlannerTokens = 0,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    SuggestedQuestions = [], //suggestedQuestions,
                };
                
                // Save the conversation
                await SaveConversationAsync(conversation, result);

                this.logger?.LogInformation($"Total execution time: {result.RequestTime} seconds");
                this.logger?.LogInformation($"DocumentBusinessLogic.QueryReportsAsync finished");
                return new Result<QueryResult>(result);
            }
            catch (Exception ex)
            {

                return Error<QueryResult>(ex, null);
            }
        }



        /// <summary>
        /// Likes a query result
        /// </summary>
        public async Task<Result<bool>> LikeQueryResultAsync(LikeRequest request)
        {
            try
            {
                this.logger?.LogInformation("Executing DocumentBusinessLogic.LikeQueryResultAsync");
                var requestHistory = await this.dataAccess.RequestsHistory.GetAsync(request.RequestId);
                if (requestHistory != null)
                {
                    requestHistory.Liked = request.Liked ? 1 : -1;
                    requestHistory.Comments = string.IsNullOrEmpty(request.Comment) ? "" : request.Comment;
                    this.dataAccess.RequestsHistory.Update(requestHistory);

                    var conversation = await this.dataAccess.Conversations.GetAsync(request.ConversationId);
                    if (conversation != null)
                    {
                        var conversationItem = conversation.History.Where(x => x.RequestId == request.RequestId).FirstOrDefault();
                        if (conversationItem != null)
                        {
                            conversationItem.Liked = request.Liked ? 1 : -1;
                            conversationItem.Comments = string.IsNullOrEmpty(request.Comment) ? "" : request.Comment;
                            this.dataAccess.Conversations.Update(conversation);
                        }
                    }

                    await this.dataAccess.SaveChangesAsync();
                    this.logger?.LogInformation($"Like for request {request.RequestId} processed");
                    return new Result<bool>(true);
                }      
                return new Result<bool>(false, false, "unknow request id");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
                return new Result<bool>(ex.Message);
            }
        }


        /// <summary>
        /// Setups the semantic kernel
        /// </summary>
        /// <returns></returns>
        private async Task<Kernel> SetupSemanticKernelAsync(bool registerPlugins)
        {
            try
            {

                var builder = Kernel.CreateBuilder()
                        .AddAzureOpenAIChatCompletion(this.azureOpenAIChatCompletionModel, this.azureOpenAIApiEndpoint, this.azureOpenAIApiKey);
                builder.Services.AddLogging(builder =>
                {
                    builder.Services.AddSingleton(this.loggerFactory);
                });
                var kernel = builder.Build();
                kernel.Culture = System.Globalization.CultureInfo.GetCultureInfo("es-CL");

                if (registerPlugins)
                {
                    var aiplugins = await this.dataAccess.AiPlugins.GetAsync();
                    foreach (var plugin in aiplugins)
                    {
                        await kernel.ImportPluginFromOpenAIAsync(plugin.Name, new Uri(plugin.Uri), new OpenAIFunctionExecutionParameters
                        {
                            AuthCallback = async (request, pluginName, openAIAuthConfig, cancellationToken) =>
                            {

                                var header = $"Bearer {plugin.AccessToken}";
                                request.Headers.Add("Authorization", header);
                                this.logger.LogInformation($"Invoking '{request.RequestUri}'");
                                await Task.FromResult(true);
                            },                            
                        });
                        
                    }
                }
                return kernel;
            }
            catch(Exception ex)
            {
                this.logger?.LogError(ex.Message);
                return null;
            }
        }


        /// <summary>
        /// Returns the current conversation or create a new one
        /// </summary>
        private async Task<Conversation> GetConversationAsync(QueryRequest request)
        {
            this.logger?.LogInformation($"Getting saved conversation for the request");
            var conversation = (request.ConversationId.HasValue) ?
                await this.dataAccess.Conversations.GetAsync(request.ConversationId.Value) :
                null;

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    ConversationId = Guid.NewGuid(),
                    UserId = this.sessionProvider.UserId,
                    Email = this.sessionProvider.UserEmail,
                    Prompt = request.Prompt,
                    Response = "",
                    Date = DateTime.UtcNow,
                    History = [],
                };
                await this.dataAccess.Conversations.InsertAsync(conversation);
                await this.dataAccess.SaveChangesAsync();
                this.logger?.LogInformation($"Saved new conversation {conversation.ConversationId}");
            }
            return conversation;
        }


        /// <summary>
        /// Saves the response as part of the conversation
        /// </summary>
        private async Task<bool> SaveConversationAsync(Conversation conversation, QueryResult result)
        {
            this.logger?.LogInformation($"Updating conversation {conversation.ConversationId} history");
            var history = new ConversationHistory
            {
                ConversationId = result.ConversationId,
                RequestId = result.RequestId,
                Prompt = result.Prompt,
                Response = result.Summary,
                RequestTime = result.RequestTime,
                CompletionTokens = result.CompletionTokens,
                SuggestedQuestions = result.SuggestedQuestions,
                PlannerTokens = result.PlannerTokens,
                PromptTokens = result.PromptTokens,
                RequestDate = result.RequestDate,
                SuggestedPrompt = result.SuggestedPrompt,
                Tokens = result.Tokens,
                Liked = 0,
                Comments = string.Empty,
            };
            conversation.History.Add(history);
            this.dataAccess.Conversations.Update(conversation);

            this.logger?.LogInformation($"Saving request history");
            await this.dataAccess.RequestsHistory.InsertAsync(new RequestHistory
            {
                ConversationId = result.ConversationId,
                RequestId = result.RequestId,
                Prompt = conversation.Prompt,
                Response = result.Summary,
                RequestTime = result.RequestTime,
                CompletionTokens = result.CompletionTokens,
                SuggestedQuestions = result.SuggestedQuestions,
                PlannerTokens = result.PlannerTokens,
                PromptTokens = result.PromptTokens,
                RequestDate = result.RequestDate,
                SuggestedPrompt = result.SuggestedPrompt,
                Tokens = result.Tokens,
                Liked = 0,
                Comments = string.Empty,
                Email = this.sessionProvider.UserEmail,
                UserId = this.sessionProvider.UserId,
            });

            await this.dataAccess.SaveChangesAsync();
            return true;
        }


        /// <summary>
        /// Tries to infer the prompt from conversation
        /// </summary>       
        private async Task<string> GetSuggestedPromptFromConversationAsync(Kernel kernel, QueryRequest request, Conversation conversation)
        {

            var year = DateTime.Now.Year;
            var monthYear = DateTime.Now.ToString("MM-yyyy");
            var lastMonthYear = DateTime.Now.AddMonths(-1).ToString("MM-yyyy");
            var date = DateTime.Now.ToString("dd-MM-yyyy");

            // Chat history
            var chatHistory = string.Empty;
            if (conversation.History != null && conversation.History.Any())
            {
                var count = 0;
                var lastestQuestion = conversation.History.OrderByDescending(x => x.RequestDate).ToList();
                lastestQuestion = lastestQuestion.OrderBy(x => x.RequestDate).ToList();
                foreach (var item in lastestQuestion)
                {
                    chatHistory += $"\n -'user: {item.SuggestedPrompt}?'";
                    count++;
                    if (count == 4) break;
                }
            }

            var prompt = request.Prompt.Replace("?", "");
            prompt = prompt.Replace("¿", "");

            chatHistory += $"\n-'user: {prompt}'";

            string suggestedQuestionsPrompt = $"""
              Hoy es {date}.
              Infiere la pregunta del usuario a partir de: '{prompt}'.
              Considera el siguiente historial de preguntas del usuario: {chatHistory}.           
              Responder como una pregunta en una sola línea.
              """;

            var suggestedQueryFunction = kernel.CreateFunctionFromPrompt(suggestedQuestionsPrompt,
                new OpenAIPromptExecutionSettings()
                {
                    MaxTokens = 400,
                    Temperature = 0,
                    TopP = 0.95,
                });

            var suggestedQueryAnswer = await kernel.InvokeAsync(suggestedQueryFunction);
            var suggestedQuery = suggestedQueryAnswer.GetValue<string>();

            logger?.LogInformation($"Suggested query: {suggestedQuery}");

            return string.IsNullOrEmpty(suggestedQuery) ? string.Empty : suggestedQuery;
        }


        /// <inheritdoc/>
        public async Task<Result<QueryResult>> GetSuggestedPrompt(QueryRequest request)
        {
            try
            {
                // Query the Azure Open AI
                var kernel = await SetupSemanticKernelAsync(false);
                var conversation = await GetConversationAsync(request);
                var suggestedPrompt = await GetSuggestedPromptFromConversationAsync(kernel, request, conversation);
                return new Result<QueryResult>(new QueryResult
                {
                    Prompt = request.Prompt,
                    SuggestedPrompt = suggestedPrompt,
                });
             
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
                return new Result<QueryResult>(ex.Message);
            }
        }



        /// <summary>
        /// Remove invalid characters from the text 
        /// </summary>    
        private static string CleanUpText(string text)
        {
            var result = text;
            result = result.Replace("..", ".");
            result = result.Replace(". .", ".");       
            result = result.Replace("\n", "");
            result = result.Replace("?", "");
            result = result.Replace("¿", "");
            result = result.Replace("á", "a");
            result = result.Replace("é", "e");
            result = result.Replace("í", "i");
            result = result.Replace("ó", "o");
            result = result.Replace("ú", "w");
            return result;
        }
    }
}
