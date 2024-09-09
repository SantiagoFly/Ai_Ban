using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Backend.Common.Logic;
using Backend.Common.Interfaces;
using Backend.Models;
using Microsoft.SemanticKernel;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.DependencyInjection;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using AI.Dev.OpenAI.GPT;
using Azure.Search.Documents.Indexes;
using DnsClient.Internal;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;

namespace DocumentSearch.BusinessLogic
{
    /// </inheritdoc/>
    public class QueryDocumentsPluginLogic : BaseLogic, IQueryDocumentsPluginLogic
    {       
        private readonly string azureOpenAIChatCompletionModel;
        private readonly OpenAIClient openAIClient;
        private readonly SearchClient searchClient;
        private readonly string azureOpenAIEmbeddingModel;
        private readonly int maxTokenForSearch = 15000;
        private readonly Microsoft.Extensions.Logging.ILoggerFactory loggerFactory;
        private readonly string azureOpenAIApiEndpoint;
        private readonly string azureOpenAIApiKey;


        /// <summary>
        /// Gets by DI the dependeciees
        /// </summary>
        /// <param name="dataAccess"></param>
        public QueryDocumentsPluginLogic(ISessionProvider sessionProvider,
            IDataAccess dataAccess, IConfiguration configuration,  ILogger<QueryDocumentsPluginLogic> logger, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) : base(sessionProvider, dataAccess, logger)
        {         
            this.azureOpenAIApiKey = configuration["AzureOpenAIApiKey"];
            this.azureOpenAIApiEndpoint = configuration["AzureOpenAIApiEndpoint"];
            var azureSearchEndpoint = configuration["AzureSearchEndpoint"];
            var azureSearchKey = configuration["AzureSearchKey"];
            var azureSearchIndex = configuration["AzureSearchIndex"];
            this.loggerFactory = loggerFactory;
            this.azureOpenAIChatCompletionModel = configuration["AzureOpenAIChatCompletionModel"];
                     
            this.azureOpenAIEmbeddingModel = configuration["AzureOpenAIEmbeddingModel"];
            this.openAIClient = new OpenAIClient(new Uri(this.azureOpenAIApiEndpoint), new AzureKeyCredential(this.azureOpenAIApiKey));

            var indexClient = new SearchIndexClient(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchKey));
            this.searchClient = indexClient.GetSearchClient(azureSearchIndex);        

        }


      
        /// <inheritdoc/>
        public async Task<string> QueryDocumentsAsync(string question)
        {
            try
            {
                var documentsLinks = string.Empty;
                var relatedDocuments = await SearchForRelatedDocumentsAsync(question);
                var additionInformation = relatedDocuments.Item2;
                logger?.LogInformation($"Executing the QueryDocumentsAsync: {question}");

                var builder = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(this.azureOpenAIChatCompletionModel, this.azureOpenAIApiEndpoint, this.azureOpenAIApiKey);
                builder.Services.AddLogging(builder =>
                {
                    builder.Services.AddSingleton(loggerFactory);
                });

                var kernel = builder.Build();

            
                var kernelPrompt = additionInformation.Length >0 ?  $"""
                    Responder en español de forma amigable y concisa la  siguiente consulta: ¿'{question}'?                
                    Utilizando la la siguiente información: {additionInformation}.     
                    No consultes internet para buscar información adicional.
                   """
                 : $"""
                    Responder en español de forma amigable que no tienes información para responder la siguiente consulta: ¿'{question}'?
                   """;

                var functionResult = await kernel.InvokePromptAsync(kernelPrompt, new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 800,
                    TopP = 0.5
                }));

                var result = functionResult.GetValue<string>();
                if (relatedDocuments.Item1.Count > 0)
                {
                    var stringBuilder = new StringBuilder(result);
                    var scheme = Uri.UriSchemeHttps + Uri.SchemeDelimiter;
                    stringBuilder.AppendLine("\nPuedes encontrar mayor información en los siguientes documentos:");
                    foreach (var document in relatedDocuments.Item1)
                    {
                        stringBuilder.AppendLine($"\n- [{document.File}]({document.DocumentUri})");
                    }
                    result = stringBuilder.ToString();
                }

                return result;

            }
            catch (Exception ex)
            {
                Error<string>(ex, string.Empty);
            }
            return string.Empty;
        }



        /// <summary>
        /// Search the related documents 
        /// </summary>
        private async Task<(List<DocumentDetails>, string)> SearchForRelatedDocumentsAsync(string prompt)
        {
            var resultDocumentDetails = new List<DocumentDetails>();
            var queryEmbeddings = await GetEmbeddingsAsync(prompt);

            var searchOptions = new SearchOptions
            {
                Select = { "title", "content", "fileUri", "folder" },
                SearchFields = { "title", "content" },
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new()
                {
                    SemanticConfigurationName = "default",
                    QueryCaption = new(QueryCaptionType.Extractive),
                    QueryAnswer = new(QueryAnswerType.Extractive),
                },
                Size = 10,
                VectorSearch = new VectorSearchOptions
                {
                    FilterMode = VectorFilterMode.PreFilter,
                    Queries =
                    {
                        new VectorizedQuery(queryEmbeddings.ToArray())
                        {
                            KNearestNeighborsCount = 5,
                            Exhaustive = false,
                            Fields = { "titleVector", "contentVector" }
                        }
                    }
                },
            };

            logger?.LogInformation($"getting a sas token for the documents");
        
            var tolerance = 1.8;
            var text = string.Empty;
            logger?.LogInformation($"Searching index for related documents");

            SearchResults<SearchDocument> searchResponse = searchClient.Search<SearchDocument>(prompt, searchOptions);
            logger?.LogInformation($"Results from the semantic search");
            foreach (QueryAnswerResult result in searchResponse.SemanticSearch.Answers)
            {
                logger?.LogInformation($"Answer Highlights: {result.Highlights}");
                logger?.LogInformation($"Answer Text: {result.Text}");
            }

            int count = 0;
            logger?.LogInformation($"Results from the vector search");
            await foreach (SearchResult<SearchDocument> searchResult in searchResponse.GetResultsAsync())
            {
                var title = searchResult.Document["title"].ToString();
                var content = searchResult.Document["content"].ToString();
                content = content.Replace("\n", "");
                var file = searchResult.Document["title"].ToString();
                var fileUri = $"{searchResult.Document["fileUri"]}";

                logger?.LogInformation($"Answer RerankerScore: {searchResult.SemanticSearch.RerankerScore} ");
                if (searchResult.SemanticSearch.RerankerScore < tolerance)
                {

                    logger?.LogInformation($"Skipping '{title}' result because is lower than the : {tolerance} tolerance");
                    continue;
                }

                // Extract the document from the metadata
                var score = searchResult.Score;
                var rerankerScore = searchResult.SemanticSearch.RerankerScore;

                var documentDetails = resultDocumentDetails.Find(x => x.File == file);

                var citation = new DocumentDetailCitation
                {
                    Content = content,
                    Score = score.Value,
                    RerankScore = rerankerScore.Value
                };

                if (documentDetails == null)
                {
                    documentDetails = new DocumentDetails
                    {
                        File = file,
                        DocumentUri = fileUri,
                        Citations = []
                    };

                    documentDetails.Citations.Add(citation);
                    resultDocumentDetails.Add(documentDetails);

                    this.logger?.LogInformation($"Added '{title}' as reference (search result)");
                }
                else
                {
                    documentDetails.Citations.Add(citation);
                }

                var textTokens = GPT3Tokenizer.Encode(text).Count;
                var contentTokens = GPT3Tokenizer.Encode(content).Count;
                if (contentTokens + textTokens > this.maxTokenForSearch)
                {
                    break;
                }

                var stringBuilder = new StringBuilder(text);
                stringBuilder.Append('\n').Append(content);
                text = stringBuilder.ToString();

                count++;
            }
            logger?.LogInformation($"{count} documents found");
            return (resultDocumentDetails, text);
        }


        /// <summary>
        /// Generates and embedding for a text
        /// </summary>
        private async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string text)
        {
            try
            {
                var cleanUpText = CleanUpTextForEmbeddings(text);
                var response = await openAIClient.GetEmbeddingsAsync(new EmbeddingsOptions(this.azureOpenAIEmbeddingModel, [cleanUpText]));
                var queryEmbeddings = response.Value.Data[0].Embedding;
                return queryEmbeddings;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error generating prompt embedding: {ex.Message}");
                throw;
            }
        }


        /// <summary>
        /// Remove invalid characters from the text for embeddings
        /// </summary>    
        private static string CleanUpTextForEmbeddings(string text)
        {
            var result = text;
            result = result.Replace("..", ".");
            result = result.Replace(". .", ".");
            result = result.Replace("\n", "");
            return result;
        }
    }


}
