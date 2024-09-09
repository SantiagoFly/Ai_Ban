using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Backend.Common.Interfaces;
using Backend.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Azure.Storage.Queues;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;



namespace Backend.DataAccess
{
    /// <inheritdoc/>
    public class DataAccess : IDisposable, IDataAccess
    {
        private bool disposed = false;
        private readonly DatabaseContext context;        
        private readonly string storageConnectionString;
        private readonly BlobServiceClient blobServiceClient;
        private readonly CosmosClient cosmosClient;
        private readonly string databaseId;

        private readonly QueueClientOptions queueClientOptions = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };


        /// <inheritdoc/>
        public IRepository<Models.User> Users { get; }


        /// <inheritdoc/>
        public IRepository<Conversation> Conversations { get; }


        /// <inheritdoc/>
        public IRepository<RequestHistory> RequestsHistory { get; }


        /// <inheritdoc/>
        public IRepository<StartUpOption> StartUpOptions { get; }


        /// <inheritdoc/>
        public IRepository<LibraryStructure> LibraryStructure { get; }


        /// <inheritdoc/>
        public IRepository<AiPlugin> AiPlugins { get; }


        /// <summary>
        /// Gets the configuration
        /// </summary>
        public DataAccess(IConfiguration configuration)
        {
            this.context = new DatabaseContext(configuration);
            this.Users = new Repository<Models.User>(context);
            this.Conversations = new Repository<Conversation>(context);
            this.RequestsHistory = new Repository<RequestHistory>(context);
            this.StartUpOptions = new Repository<StartUpOption> (context);
            this.LibraryStructure = new Repository<LibraryStructure>(context);
            this.AiPlugins = new Repository<AiPlugin>(context);
            this.context.Database.EnsureCreated();
            this.storageConnectionString = configuration["StorageConnectionString"];
            this.blobServiceClient = new BlobServiceClient(this.storageConnectionString);
            this.cosmosClient = new CosmosClient(configuration["DataBaseConnectionString"]);
            this.databaseId = configuration["DatabaseName"];

        }



        /// <inheritdoc/>
        public Task<int> SaveChangesAsync()
        {
            return context.SaveChangesAsync();
        }



        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && !disposing)
            {              
                context.Dispose();
            }
            this.disposed = true;
        }


        /// <inheritdoc/>
        public string GetSasToken(string container, int expiresOnMinutes)
        {
            // Generates the token for this account
            var accountKey = string.Empty;
            var accountName = string.Empty;
            var connectionStringValues = this.storageConnectionString.Split(';')
                .Select(s => s.Split(['='], 2))
                .ToDictionary(s => s[0], s => s[1]);
            if (connectionStringValues.TryGetValue("AccountName", out var accountNameValue) && !string.IsNullOrWhiteSpace(accountNameValue)
                && connectionStringValues.TryGetValue("AccountKey", out var accountKeyValue) && !string.IsNullOrWhiteSpace(accountKeyValue))
            {
                accountKey = accountKeyValue;
                accountName = accountNameValue;

                var storageSharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
                var blobSasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = container,
                    ExpiresOn = DateTime.UtcNow + TimeSpan.FromMinutes(expiresOnMinutes)
                };

                blobSasBuilder.SetPermissions(BlobAccountSasPermissions.All);
                var queryParams = blobSasBuilder.ToSasQueryParameters(storageSharedKeyCredential);
                var sasToken = queryParams.ToString();
                return sasToken;
            }
            return string.Empty;
        }



        /// <inheritdoc/>      
        public async Task<bool> AddMessageToQueueAsync(string queueName, string message)
        {
            var queueClient = new QueueClient(storageConnectionString, queueName, this.queueClientOptions);
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(message);
            return true;
        }


        /// <inheritdoc/>
        public async Task<bool> DeleteQueueAsync(string queueName)
        {
            var queueClient = new QueueClient(storageConnectionString, queueName, this.queueClientOptions);
            return await queueClient.DeleteIfExistsAsync();
        }


        /// <inheritdoc/>    
        public async Task<string> GetJsonFileAsync(string container, string folder, string fileName)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(container);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient($"{folder}/{fileName}.json");
            if (!await blobClient.ExistsAsync()) return string.Empty;

            var response = await blobClient.DownloadAsync();
            using var streamReader = new StreamReader(response.Value.Content);
            return await streamReader.ReadToEndAsync();
        }


        /// <inheritdoc/>
        public async Task<bool> SaveJsonFileAsyncAsync(string container, string folder, string fileName, string content)
        {

            var containerClient = blobServiceClient.GetBlobContainerClient(container);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient($"{folder}/{fileName}.json");

            byte[] byteArray = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(byteArray);
            stream.Position = 0;
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders()
            };
            uploadOptions.HttpHeaders.ContentType = "application/json";
            await blobClient.UploadAsync(stream, uploadOptions);
            return true;
        }

        public async Task<IEnumerable<StartUpOption>> GetAllAsync()
        { 
            string containerId = "startup-options";

            var container = this.cosmosClient.GetContainer(databaseId, containerId);

            // Filtro para obtener solo los campos necesarios
            var query = new QueryDefinition("SELECT c.Title, c.Color, c.Text, c.Icon FROM c");

            var resultSetIterator = container.GetItemQueryIterator<StartUpOption>(query);

            var results = new List<StartUpOption>();
            while (resultSetIterator.HasMoreResults)
            {
                var response = await resultSetIterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }
    }


}
