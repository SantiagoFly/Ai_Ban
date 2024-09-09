using AI.Dev.OpenAI.GPT;
using AngleSharp.Css.Dom;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Models;
using DnsClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Backend.Embedding.Logic
{
    public readonly record struct PageDetail(
        int Index,
        int Offset,
        string Text);

    public readonly record struct Section(
        string Id,
        string Content,
        string Filename,
        string Page,
        string FileUri,
        string Title,
        string Folder,
        string[] GroupIds,
        string[] Tags,
        string Category,
        string Year,
        int PageNumber);


    public partial class EmbeddingsBusinessLogic : IEmbeddingsBusinessLogic
    {
        [GeneratedRegex("[^0-9a-zA-Z_-]")]
        private static partial Regex matchInSetRegex();
        private const int modelDimensions = 3072;
        private readonly string azureSearchIndex;
        private readonly SearchClient searchClient;
        private readonly SearchIndexClient indexClient;
        private readonly ILogger<EmbeddingsBusinessLogic> logger;
        private readonly DocumentAnalysisClient documentAnalysisClient;
        private readonly BlobContainerClient blobContainerClient;
        private readonly QueueClient libraryToProcessQueueClient;
        private readonly QueueClient failedDocumentsQueueClient;
        private readonly string documentsStorageConnectionString;
        private readonly IDataAccess dataAccess;
        private readonly string azureOpenAIEmbeddingModel;
        private readonly string libraryClientId;
        private readonly string libraryTenantId;
        private readonly string libraryCertificatePath;
        private readonly string libraryCertificatePassword;
        private readonly int tokenOverlap = 200;
        private readonly int numTokens = 2048;
        private readonly string azureOpenAIApiKey;
        private readonly string azureOpenAIApiEndpoint;
        private readonly OpenAIClient openAIClient;
       


        /// <summary>
        /// Gets by DI the dependeciees
        /// </summary>
        /// <param name="dataAccess"></param>
        public EmbeddingsBusinessLogic(IDataAccess dataAccess, IConfiguration configuration, ILogger<EmbeddingsBusinessLogic> logger)
        {
            this.logger = logger;
            this.dataAccess = dataAccess;
            this.azureOpenAIEmbeddingModel = configuration["AzureOpenAIEmbeddingModel"];
            var azureSearchEndpoint = configuration["AzureSearchEndpoint"];
            var azureSearchKey = configuration["AzureSearchKey"];

            this.azureSearchIndex = configuration["AzureSearchIndex"];
            this.documentsStorageConnectionString = configuration["DocumentsStorageConnectionString"];
            this.azureOpenAIApiKey = configuration["AzureOpenAIApiKey"];
            this.azureOpenAIApiEndpoint = configuration["AzureOpenAIApiEndpoint"];
            this.indexClient = new SearchIndexClient(new Uri(azureSearchEndpoint), new AzureKeyCredential(azureSearchKey));
            this.searchClient = indexClient.GetSearchClient(this.azureSearchIndex);

            this.documentAnalysisClient = new DocumentAnalysisClient(new Uri(configuration["AzureFormRecognizerEndpoint"]), new AzureKeyCredential(configuration["AzureFormRecognizerKey"]));
            this.blobContainerClient = new BlobContainerClient(configuration["DocumentsStorageConnectionString"], configuration["DocumentStorageContainer"]);
    
            var options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            };

            this.libraryToProcessQueueClient = new QueueClient(configuration["DocumentsStorageConnectionString"], "library-to-process", options);
            this.failedDocumentsQueueClient = new QueueClient(configuration["DocumentsStorageConnectionString"], "failed-library-to-process", options);            
            this.openAIClient = new OpenAIClient(new Uri(this.azureOpenAIApiEndpoint), new AzureKeyCredential(this.azureOpenAIApiKey));
            
            this.libraryClientId = configuration["LibraryClientId"].ToString();
            this.libraryTenantId = configuration["LibraryTenantId"].ToString();
            this.libraryCertificatePassword = configuration["LibrarySecret"].ToString();
            this.libraryCertificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "certificate.pfx");
        }


        /// </inheritdoc/>
        public async Task<bool> CheckDocumentsForEmbeddingQueueAsync()
        {
            try
            {
                var maxDocumentsToUpdate = 5;
                var addedDocumentsToProcess = 0;
                var libraryStructure = await this.dataAccess.LibraryStructure.GetAsync();
                await this.libraryToProcessQueueClient.CreateIfNotExistsAsync();
                _ = libraryStructure.Select(async library =>
                {
                    if (library.Documents != null)
                    {
                        var documents = library.Documents.Where(x => x.ToBeUpdated).ToList();
                        var processDocumentsMessages = (await this.libraryToProcessQueueClient.PeekMessagesAsync(10)).Value.ToList();
                        if (processDocumentsMessages.Count < maxDocumentsToUpdate)
                        {
                            var currentDoumentsCount = processDocumentsMessages.Count;
                            if (currentDoumentsCount > 0)
                            {
                                addedDocumentsToProcess += currentDoumentsCount;
                            }

                            foreach (var document in documents)
                            {
                                if (addedDocumentsToProcess >= maxDocumentsToUpdate) break;
                                await this.libraryToProcessQueueClient.SendMessageAsync(JsonConvert.SerializeObject(document));
                                addedDocumentsToProcess++;
                            }

                        }
                    }
                }).ToList();
                return true;              
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Failed add item to the pending queue");
                return false;
            }
        }


        /// <summary>
        /// Creates the embedding for a file and save it on the Azure Search
        /// </summary>
        public async Task<bool> CreateEmbeddingsAsync(string queuedItem)
        {
            var request = JsonConvert.DeserializeObject<EmbeddingRequest>(queuedItem);
            try
            {
                var result = await CreateEmbeddingsAsync(request);
                if (!string.IsNullOrEmpty(result))
                {
                    await this.failedDocumentsQueueClient.CreateIfNotExistsAsync();
                    await this.failedDocumentsQueueClient.SendMessageAsync($"{queuedItem} : {result}");
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, $"Failed to embed {request.Filename}, added to the failed-documents queue");
                return false;
            }
        }



        /// <summary>
        /// Creates the embedding for a file and save it on the Azure Search
        /// </summary>
        public async Task<Result<string>> CreateEmbeddingsForFileAsync(EmbeddingRequest request)
        {
            try
            {
                var result = await CreateEmbeddingsAsync(request);
                return new Result<string>(result, string.IsNullOrEmpty(result), "");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Failed to embed {request.Filename}");
                return new Result<string>(string.Empty, false, ex.Message);
            }
        }


        /// <summary>
        /// Creates the embedding for a file and save it on the Azure Search
        /// </summary>
        public async Task<string> CreateEmbeddingsAsync(EmbeddingRequest request)
    {
            try
            {
                if (request.ToBeUpdated)
                {
                
                    await EnsureSearchIndexAsync(this.azureSearchIndex);

                    // Saves the file on the destination container
                    this.logger.LogInformation($"Copying '{request.Filename}' from the library to the library-to-process container", request.Filename);
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(request.Filename);

                    var fileData = DownloadFileFromSharepoint(request.SiteUri, request.Uri);
                    if (fileData == null) return $"@Failed to embed file '{request.Filename}'";

                    var documentToProcessContainerClient = new BlobContainerClient(this.documentsStorageConnectionString, $"library-to-process");
                    var blobClient = documentToProcessContainerClient.GetBlobClient($"{request.Folder}\\{request.Filename}");

                    await documentToProcessContainerClient.CreateIfNotExistsAsync();
                    await blobClient.UploadAsync(new MemoryStream(fileData), true);
                    var sasToken = this.dataAccess.GetSasToken("library-to-process", 60);
                    var fileUri = new Uri($"{blobClient.Uri}?{sasToken}").ToString();

                    // Gets the chunks from the document
                    var chunks = await GetDocumentChunksAsync(request, fileUri);

                    // Updload each page to the converted container
                    foreach (var chunk in chunks)
                    {
                        var processFileName = $"converted/{request.Folder}/{fileNameWithoutExtension}/{fileNameWithoutExtension}-{chunk.Id}.txt";
                        //await UploadConvertedFileAsync(processFileName, chunk.Content);
                    }

                    // Index the section
                    await IndexSectionsAsync(this.azureSearchIndex, chunks, request);

                    // Saves the indexed document
                    int saveAttempts = 1;
                    while (saveAttempts < 3)
                    {
                        try
                        {

                            var libraryStructure = await this.dataAccess.LibraryStructure.GetAsync();
                            if (libraryStructure != null)
                            {
                                var library = libraryStructure.FirstOrDefault(x => x.SiteUri == request.SiteUri);
                                if (library != null)
                                {
                                    var document = library.Documents.Find(x => x.Filename == request.Filename);
                                    if (document != null)
                                    {
                                        document.ToBeUpdated = false;
                                        this.dataAccess.LibraryStructure.Update(library);
                                        await this.dataAccess.SaveChangesAsync();
                                        break;
                                    }
                                }

                            }
                        }
                        catch
                        {
                            // Tries to avoid a possible concurrency issue when accesing the DB
                            // IF this happens tries one more time
                            saveAttempts++;
                        }
                    }                                 
                    await this.dataAccess.SaveChangesAsync();      

                    // Removes the document from the "documents-to-process" container
                    try
                    {
                        await blobClient.DeleteAsync();
                    }
                    catch
                    {
                       // Ignores if this fails
                    }                  
                    this.logger.LogInformation($"{request.Filename} processed  successfully", request.Filename);
                }
                else if (request.ToBeDeleted)
                {
                    await RemoveDocumentFromIndexAsync(request);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to embed blob '{BlobName}'", request.Filename);
                return ex.Message;
            }
        }


        /// <summary>
        /// Gets the file from sharepoint
        /// </summary>       
        private byte[] DownloadFileFromSharepoint(string siteUri, string fileUri)
        {
            try
            {
                byte[] data;
                var authManager = new PnP.Framework.AuthenticationManager(this.libraryClientId, this.libraryCertificatePath, this.libraryCertificatePassword, this.libraryTenantId);
                using var context = authManager.GetContext(siteUri);
                Uri filename = new(fileUri);
                var file = context.Web.GetFileByServerRelativeUrl(filename.AbsolutePath);
                var stream = file.OpenBinaryStream();
                context.Load(file);
                context.ExecuteQuery();

                using (var memoryStream = new MemoryStream())
                {
                    stream.Value.CopyTo(memoryStream);
                    data = memoryStream.ToArray();
                }
                return data;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Unable to download file '{fileUri}': {ex.Message}");
            }
            return [];
        }


        /// <summary>
        /// Creates the index
        /// </summary>
        private async Task EnsureSearchIndexAsync(string searchIndexName)
        {
            try
            {
                var vectorSearchProfile = "hnsw-profile";
                var vectorSearchConfigName = "nsw-config";
                var indexNames = this.indexClient.GetIndexNamesAsync();
                await foreach (var page in indexNames.AsPages())
                {
                    if (page.Values.Any(indexName => indexName == searchIndexName))
                    {
                        this.logger.LogInformation("Search index '{SearchIndexName}' already exists", searchIndexName);
                        return;
                    }
                }

                var index = new SearchIndex(searchIndexName)
                {
                    Fields =
                    {
                        new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                        new SearchableField("title") {IsFilterable = true, IsSortable = true},
                        new SearchableField("content") { AnalyzerName = "es.microsoft", IsFilterable = true },
                        new SearchableField("category") { IsFilterable = true, IsSortable = true, IsFacetable = true,  },
                        new SearchableField("folder") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                        new SimpleField("year", SearchFieldDataType.String) { IsFacetable = true },
                        new SimpleField("page", SearchFieldDataType.String) { IsFacetable = true },
                        new SimpleField("fileUri", SearchFieldDataType.String) { IsFacetable = true, IsFilterable = true  },
                        new SearchField("groupIds", SearchFieldDataType.Collection(SearchFieldDataType.String))
                        {
                            IsFilterable = true,
                            IsHidden = true,
                        },
                        new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String))
                        {
                            IsFilterable = true,
                        },
                        new SearchField("titleVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                        {
                            IsSearchable = true,
                            VectorSearchDimensions = modelDimensions,
                            VectorSearchProfileName = vectorSearchProfile,
                        },
                        new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                        {
                            IsSearchable = true,
                            VectorSearchDimensions = modelDimensions,
                            VectorSearchProfileName = vectorSearchProfile,
                        },
                    },
                    VectorSearch = new()
                    {
                        Algorithms =
                    {
                            new HnswAlgorithmConfiguration(vectorSearchConfigName)
                    },
                        Profiles =
                    {
                        new VectorSearchProfile(vectorSearchProfile, vectorSearchConfigName)
                    }
                    },
                    SemanticSearch = new SemanticSearch
                    {
                        Configurations =
                        {
                            new SemanticConfiguration("default", new SemanticPrioritizedFields
                            {
                                TitleField = new SemanticField( "title"),
                                ContentFields =
                                {
                                        new SemanticField("content")
                                }
                            })
                        }
                    }
                };

                this.logger.LogInformation("Creating '{searchIndexName}' search index", searchIndexName);
                await this.indexClient.CreateIndexAsync(index);
            }
            catch
            {
                // Could be a conflict creating the index (so it validades, if it exists, it continues)
                try
                {
                    var indexNames = this.indexClient.GetIndexNamesAsync();
                    await foreach (var page in indexNames.AsPages())
                    {
                        if (page.Values.Any(indexName => indexName == searchIndexName))
                        {
                            this.logger.LogInformation("Search index '{SearchIndexName}' already exists", searchIndexName);
                            return;
                        }
                    }
                }
                catch (Exception ex2)
                {
                    this.logger.LogError($"Failed to create the index: {ex2.Message}", searchIndexName);
                }
            }
        }


        /// <summary>
        /// Extracts the text from the document
        /// </summary>
        private async Task<IReadOnlyList<Section>> GetDocumentChunksAsync(EmbeddingRequest request, string fileUri)
        {
            
            var fileWithoutExtension = Path.GetFileNameWithoutExtension(request.Filename);
            var content = await this.dataAccess.GetJsonFileAsync("recognized", fileWithoutExtension, fileWithoutExtension);

            // Checks if the file was previously processed
            if (string.IsNullOrEmpty(content))
            {                               
                this.logger.LogInformation($"Extracting text from '{request.Filename}' using Azure Form Recognizer", request.Filename);
                var options = new AnalyzeDocumentOptions
                {
                    Locale = "es-ES",
                };
              
                
                var model = "prebuilt-layout";
                if (string.Equals(Path.GetExtension(request.Filename), ".pdf"))
                {
                    options.Features.Add(DocumentAnalysisFeature.OcrHighResolution);
                }

                var operation = this.documentAnalysisClient.AnalyzeDocumentFromUri(WaitUntil.Started, model, new Uri(fileUri), options);


                var analyzeResults = await operation.WaitForCompletionResponseAsync();
                content = analyzeResults.Content.ToString();

                this.logger.LogInformation($"Saving result from form recognizer");
                await this.dataAccess.SaveJsonFileAsyncAsync("recognized",
                      fileWithoutExtension, fileWithoutExtension, content);
            }

            // invoke deserialization via reflection
            var jsonElement = JsonDocument.Parse(content).RootElement;
            var jsonElementAnalyzeResult = jsonElement.GetProperty("analyzeResult"); 
            var methodInfo = typeof(AnalyzeResult).GetMethod("DeserializeAnalyzeResult", BindingFlags.NonPublic | BindingFlags.Static);
            _ = (AnalyzeResult)methodInfo.Invoke(null, [jsonElementAnalyzeResult]);

            int chunkId = 0;
            var minChunkSize = 100;
            var sections = new List<Section>();

            int pageNumber = 0;
            JToken document = JToken.Parse(jsonElementAnalyzeResult.ToString());

            if (document["pages"] !=null)
            {
                var pageCount = document["pages"].Count();
                if (pageNumber > 100)
                {
                    this.logger.LogInformation($"Document has {pageCount} pages. Please consider splitting it into smaller documents of 100 pages.");
                }            
            }

            var processedTables = new List<int>();
            if (document["tables"] != null)
            {
                var tableIndex = 0;
                foreach (var table in document["tables"])
                {
                    if (!processedTables.Contains(tableIndex))
                    {
                        
                        processedTables.Add(tableIndex);
                        var tableContent = TableToHtml(table);
                        chunkId++;
                        tableIndex++;

                        // page logic
                        pageNumber = 1;
                        var boundingRegions = table["cells"][0]["boundingRegions"];
                        if (boundingRegions != null)
                        {
                            pageNumber = int.Parse(boundingRegions[0]["pageNumber"].ToString());
                        }

                        this.logger.LogInformation($"Recognized the following table on page {pageNumber}.");
                        this.logger.LogInformation($"{tableContent}");

                        // if there is text before the table add it to the beggining of the chunk to improve context.
                        var beforeText = GetTextBeforeTable(document, table, document["tables"]);
                        this.logger.LogInformation(string.IsNullOrEmpty(beforeText) ? "No text found before the table" : $"Text recognized before the table: {beforeText}");
                        tableContent = beforeText + "\n" + tableContent;
                     
                        // if there is text after the table add it to the end of the chunk to improve context.
                        var afterText = GetTextAfterTable(document, table, document["tables"]);
                        this.logger.LogInformation(string.IsNullOrEmpty(afterText) ? "No text found after the table" : $"Text recognized after the table: {afterText}");
                        tableContent = tableContent + "\n" + afterText;

                        var section = CreateSection(request, chunkId, tableContent, pageNumber);
                        sections.Add(section);
                    }
                }
            }


            if (document["paragraphs"] != null)
            {
                var paragraphContent = string.Empty;
                int chunkSize;
                foreach (var paragraph in document["paragraphs"])
                {
                    pageNumber = paragraph["boundingRegions"] != null ? int.Parse(paragraph["boundingRegions"][0]["pageNumber"].ToString()) : 1;
                    if (!IsParagraphInTable(paragraph, document["tables"]))
                    {
                        chunkSize = GPT3Tokenizer.Encode(paragraphContent + paragraph["content"].ToString()).Count;
                        if (chunkSize < numTokens)
                        {
                            paragraphContent = paragraphContent + "\n" + paragraph["content"].ToString();
                        }
                        else
                        {
                            chunkId++;
                            var section = CreateSection(request, chunkId, paragraphContent, pageNumber);
                            sections.Add(section);

                            // overlap logic                    
                            var overlappedText = paragraphContent.Split([' ']);
                            overlappedText = overlappedText.Skip(overlappedText.Length - (int)Math.Round(tokenOverlap / 0.75)).ToArray();
                            paragraphContent = string.Join(' ', overlappedText);
                        }
                    }

                }

                // last seccion
                chunkId++;
                chunkSize = GPT3Tokenizer.Encode(paragraphContent).Count;
                if (chunkSize > minChunkSize)
                {
                    var section = CreateSection(request, chunkId, paragraphContent, pageNumber);
                    sections.Add(section);
                }
            }

           return sections;
        }


        /// <summary>
        /// Find the text before the table
        /// </summary>      
        private string GetTextBeforeTable(JToken document, JToken table, JToken tables)
        {
            JToken firstCell;
            var textBeforeTable = string.Empty;
            int firstCellOffset = int.MaxValue;
            try
            {               
                // get first cell
                foreach (var cell in table["cells"])
                {
                    // Check if the cell has content
                    if (cell["content"] != null)
                    {
                        // Get the offset of the cell content
                        firstCell = cell; 
                        if (cell["spans"] != null && cell["spans"].Any())
                        {
                            // Get the offset of the first span the first span is the one that contains the content    
                            firstCellOffset = (int)cell["spans"][0]["offset"];
                            break;
                        }                      
                   
                    }
                }


                int textBeforeOffset = Math.Max(0, firstCellOffset - this.tokenOverlap);
                var content = document["content"].ToString();

                // we don't want to add text before the table if it is contained in the table
                for (int idx = 0; idx < content[textBeforeOffset..firstCellOffset].Length; idx++)
                {
                    if (!IsCharInATable(textBeforeOffset + idx, tables))
                    {
                        textBeforeTable += document["content"].ToString()[textBeforeOffset + idx];
                    }
                }
                return textBeforeTable.Trim();
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting text after table: {ex.Message}");
            }
            return string.Empty;
        }


        /// <summary>
        /// Find the text after the table
        /// </summary>      
        private string GetTextAfterTable(JToken document, JToken table, JToken tables)
        {
            var lastCellOffset = 0;
            JToken lastCell = null;
            var textAfterTable = string.Empty;
            var maxTokenOverlap = this.tokenOverlap;
            try
            {
                
                // get last cell
                foreach (var cell in table["cells"])
                {
                    // Check if the cell has content
                    if (cell["content"] != null)
                    {
                        // Get the offset of the cell content
                        lastCell = cell;
                        if (cell["spans"] != null && cell["spans"].Any())
                        {
                            lastCellOffset = (int)cell["spans"][0]["offset"];
                        }                       
                    }
                }

                // we don't want to add text after the table if it is contained in a table
                string content = document["content"].ToString();
                int textAfterOffset = lastCellOffset + (lastCell["content"] != null ? lastCell["content"].ToString().Length : 0);


                if (textAfterOffset + this.tokenOverlap > content.Length)
                {
                    maxTokenOverlap = content.Length - textAfterOffset;
                    maxTokenOverlap = Math.Max(0, maxTokenOverlap);
                }

                int textLength = content.Substring(textAfterOffset, maxTokenOverlap).Length;

                for (int idx = 0; idx < textLength; idx++)
                {
                    if (!IsCharInATable(textAfterOffset + idx, tables))
                    {
                        textAfterTable += document["content"].ToString()[textAfterOffset + idx];
                    }
                }
                return textAfterTable.Trim();
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting text after table: {ex.Message}");
            }
            return string.Empty;

        }

  

        /// <summary>
        /// Checks ia char is in a table
        /// </summary>
        private bool IsCharInATable(int index, JToken tables)
        {
            try
            {
                foreach (var table in tables)
                {
                    foreach (var cell in table["cells"])
                    {
                        foreach (var span in cell["spans"])
                        {
                            if ((int)span["offset"] <= index && index < (int)span["offset"] + (int)span["length"])
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;

            }
            catch (Exception ex)
            {
                logger.LogError($"Error checking if char is in a table: {ex.Message}");
            }
            return false;
             
        }


        /// <summary>
        /// Creates a seccion
        /// </summary>        
        private static Section CreateSection(EmbeddingRequest request, int chunkId, string content, int pageNumber)
        {         
            var pageName = matchInSetRegex().Replace($"{request.Filename}-{chunkId}", "_").TrimStart('_');
            return new Section(
                Id: pageName,
                Content: content,
                Filename: request.Filename,
                Title: request.Filename,
                Page: pageName,
                PageNumber: pageNumber,
                Folder: request.Folder,
                Year: request.Year,
                GroupIds: [],
                Tags: [],
                FileUri: request.Uri,
                Category: string.Empty);

        }


        /// <summary>
        /// Returns if a paragraph is inside a table
        /// </summary>
        private static bool IsParagraphInTable(JToken paragraph, JToken tables)
        {
            if (tables == null) return false;
            foreach (var table in tables)
            {
                foreach (var cell in table["cells"])
                {
                    if (cell["spans"] != null && cell["spans"].Any())
                    {
                        var cellOffset = int.Parse(cell["spans"][0]["offset"].ToString());
                        if (paragraph["spans"] != null && paragraph["spans"].Any())
                        {
                            var paragraphOffset = int.Parse(paragraph["spans"][0]["offset"].ToString());
                            if (cell["spans"] != null && cell["spans"].Any() && paragraphOffset == cellOffset)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Converts a table to html
        /// </summary>
        public static string TableToHtml(JToken table)
        {
            string tableHtml = "<table>";
            var rows = new List<List<JToken>>();
            var rowCount = int.Parse(table["rowCount"].ToString());
            for (int i = 0; i < rowCount; i++)
            {
                rows.Add([.. table["cells"].Where(cell => int.Parse(cell["rowIndex"].ToString()) == i)
                    .OrderBy(cell => int.Parse(cell["columnIndex"].ToString()))]);
            }
            foreach (var rowCells in rows)
            {
                tableHtml += "<tr>";
                foreach (var cell in rowCells)
                {
                    string tag = "td";
                    if (cell["kind"] != null)
                    {
                        if (cell["kind"].ToString() == "columnHeader" || cell["kind"].ToString() == "rowHeader")
                        {
                            tag = "th";
                        }
                    }
                    string cellSpans = "";
                    if (cell["columnSpan"] != null)
                    {
                        if (int.Parse(cell["columnSpan"].ToString()) > 1)
                        {
                            cellSpans += $" colSpan={cell["columnSpan"]}";
                        }
                    }
                    if (cell["rowSpan"] != null)
                    {
                        if (int.Parse(cell["rowSpan"].ToString()) > 1)
                        {
                            cellSpans += $" rowSpan={cell["rowSpan"]}";
                        }
                    }
                    tableHtml += $"<{tag}{cellSpans}>{HttpUtility.HtmlEncode(cell["content"])}</{tag}>";
                }
                tableHtml += " </tr>";
            }
            tableHtml += "</table>";
            return tableHtml;
        }


        /// <summary>
        /// Upload the file to the container
        /// </summary>
        private async Task UploadConvertedFileAsync(string filename, string text)
        {
            var blobClient = blobContainerClient.GetBlobClient(filename);
            if (await blobClient.ExistsAsync())
            {
                await blobClient.DeleteAsync();
            }

            this.logger.LogInformation($"Uploading file '{filename}'");
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = "text/plain"
            });
        }



        private async Task RemoveDocumentFromIndexAsync(EmbeddingRequest request)
        {
            // Remove all entries related to the same document based on the title
            this.logger.LogInformation($"Removing previous entries of {request.Filename} from the index ");
            var deleteBatch = new IndexDocumentsBatch<SearchDocument>();
            var searchResponse = this.searchClient.Search<SearchDocument>("*", new SearchOptions { Filter = $"title eq '{request.Filename}'", IncludeTotalCount = true });
            var currentdocuments = searchResponse.Value.GetResults().ToList();
            if (currentdocuments.Count != 0)
            {
                foreach (var currentdocument in currentdocuments)
                {
                    deleteBatch.Actions.Add(new IndexDocumentsAction<SearchDocument>(
                          IndexActionType.Delete, currentdocument.Document));
                }
                IndexDocumentsResult removeDocumentsResult = await this.searchClient.IndexDocumentsAsync(deleteBatch);
                this.logger.LogInformation($"Removed {deleteBatch.Actions.Count} sections from existing document");
            }

        }

        /// <summary>
        /// Adds a secction to the index
        /// </summary>
        private async Task IndexSectionsAsync(string searchIndexName, IEnumerable<Section> sections, EmbeddingRequest request)
        {

            // Remove all entries related to the same document based on the title
            await RemoveDocumentFromIndexAsync(request);
          
            // Adds the new sections
            this.logger.LogInformation("""
                Indexing sections from '{filename}' into search index '{SearchIndexName}'
                """,
                request.Filename,
                searchIndexName);

            var iteration = 0;
            var batch = new IndexDocumentsBatch<SearchDocument>();
            foreach (var section in sections)
            {
                var titleVector = await GetEmbeddingsAsync(section.Title);
                var contentVector = await GetEmbeddingsAsync(section.Content);

                batch.Actions.Add(new IndexDocumentsAction<SearchDocument>(
                    IndexActionType.MergeOrUpload,
                    new SearchDocument
                    {
                        ["id"] = section.Id,
                        ["title"] = section.Title,
                        ["content"] = section.Content,
                        ["category"] = section.Category,
                        ["page"] = section.Page,
                        ["fileUri"] = section.FileUri,
                        ["folder"] = section.Folder,
                        ["year"] = section.Year,
                        ["titleVector"] = titleVector.ToArray(),
                        ["contentVector"] = contentVector.ToArray()
                    }));

                iteration++;
                if (iteration % 1_000 is 0)
                {
                    // Every one thousand documents, batch create.
                    IndexDocumentsResult result = await this.searchClient.IndexDocumentsAsync(batch);
                    int succeeded = result.Results.Count(r => r.Succeeded);
                    this.logger.LogInformation("""
                        Indexed {Count} sections, {Succeeded} succeeded
                        """,
                            batch.Actions.Count,
                            succeeded);
                    batch = new();
                }
            }

            if (batch is { Actions.Count: > 0 })
            {
                // Any remaining documents, batch create.
                IndexDocumentsResult result = await this.searchClient.IndexDocumentsAsync(batch);
                int succeeded = result.Results.Count(r => r.Succeeded);
                this.logger.LogInformation("""
                    Indexed {Count} sections, {Succeeded} succeeded
                    """,
                    batch.Actions.Count,
                    succeeded);
            }
        }


        /// <summary>
        /// Generates and embedding for a text
        /// </summary>
        public async Task<ReadOnlyMemory<float>> GetEmbeddingsAsync(string text)
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
        /// <param name="text"></param>
        /// <returns></returns>
        private static string CleanUpTextForEmbeddings(string text)
        {
            var result = text;
            result = result.Replace("..", ".");
            result = result.Replace(". .", ".");
            result = result.Replace("\n", ".");
            return result;
        }
    }


}
