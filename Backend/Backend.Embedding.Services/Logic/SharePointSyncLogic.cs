using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using PnP.Core.Services;
using PnP.Framework;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.SharePoint.Client;
using System.Net;
using Newtonsoft.Json;
using Backend.Common.Interfaces;
using Backend.Models;

namespace Backend.Embedding.Logic
{
    /// <inheritdoc/>
    /// <summary>
    /// Gets by DI the dependeciees
    /// </summary>
    /// <param name="dataAccess"></param>
    public class SharePointSyncLogic(IDataAccess dataAccess, IConfiguration configuration, ILogger<SharePointSyncLogic> logger) : ISharePointSyncLogic
    {
        private readonly ILogger<SharePointSyncLogic> logger = logger;
        private readonly IDataAccess dataAccess = dataAccess;
        private PnPContext pnpCoreContext;

        private readonly string certificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "certificate.pfx");
        //private readonly string libraryDeleteQueue = configuration["LibraryDeleteQueue"].ToString();
        //private readonly string libraryUpdateQueue = configuration["LibraryUpdateQueue"].ToString();
        
        private readonly string clientId = configuration["LibraryClientId"].ToString();
        private readonly string tenantId = configuration["LibraryTenantId"].ToString();
        private readonly string certificatePassword = configuration["LibrarySecret"].ToString();
        private readonly string yearMetadataField = "A_x00f1_o";


        /// <summary>
        /// Gets the PnP context to connect to the sharepoint library
        /// </summary>
        /// <returns></returns>
        private async Task<PnPContext> GetCorePnPContext(string siteUri)
        {
            if (pnpCoreContext == null)
            {
                var authManager = new PnP.Framework.AuthenticationManager(this.clientId, this.certificatePath, this.certificatePassword, this.tenantId);
                using var cc = await authManager.GetContextAsync(siteUri);
                pnpCoreContext = await PnPCoreSdk.Instance.GetPnPContextAsync(cc);
            }
            return pnpCoreContext;
        }


        /// <inheritdoc/>
        public async Task CleanSyncSharepointDocumentsAsync()
        {
            try
            {
                this.logger?.LogInformation("Executing SharePointSyncBusinessLogic.CleanSyncSharepointDocumentsAsync");
                
                await this.dataAccess.LibraryStructure.GetAsync().ContinueWith((task) =>
                {
                    IEnumerable<LibraryStructure> libraryStructure = task.Result;

                    if (libraryStructure != null)
                    {
                        foreach (var item in libraryStructure)
                        {
                            this.dataAccess.LibraryStructure.Delete(item);
                        }
                    }
                });

                await this.dataAccess.SaveChangesAsync();

                //await this.dataAccess.DeleteQueueAsync(this.libraryDeleteQueue);

                //await this.dataAccess.DeleteQueueAsync(this.libraryUpdateQueue);
               
                await SyncSharepointDocumentsAsync();
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
            }
        }


        /// <inheritdoc/>
        public async Task SyncSharepointDocumentsAsync()
        {
            try
            {
                this.logger?.LogInformation("Executing SharePointSyncBusinessLogic.SyncSharepointDocumentsAsync");
              
                var authManager = new PnP.Framework.AuthenticationManager(this.clientId, this.certificatePath, this.certificatePassword, this.tenantId);

                var libraryStructure = await this.dataAccess.LibraryStructure.GetAsync();
                foreach (var library in libraryStructure)
                {
                    if (string.IsNullOrEmpty(library.SiteUri)) continue;
                    logger?.LogInformation($"Starting sync process with the sharepoint library at {library.SiteUri}");
                    using (var cc = authManager.GetContext(library.SiteUri))
                    {
                        try
                        {
                            var folder = cc.Web.GetFolderByServerRelativeUrl(library.RootFolder);
                            cc.Load(folder);
                            cc.ExecuteQuery();

                            // Recursively build the tree structure
                            var documentsInSharepoint = new Dictionary<string, EmbeddingRequest>();
                            await BuildDocumentStatus(library, "", folder, documentsInSharepoint);

                            logger?.LogInformation($"Start processing {documentsInSharepoint.Count} found documents");

                            var currentLibrary = new Dictionary<string, EmbeddingRequest>();

                      
                            if (documentsInSharepoint.Count == 0)
                            {
                                logger?.LogInformation($"No documents found in the library");
                            }
                            else
                            {
                                var currentFiles = library?.Documents;
                                if (currentFiles != null && currentFiles.Count != 0)
                                {
                                    currentFiles.ForEach(x => { currentLibrary[x.Uri] = x; });
                                }

                                var updateLibrariyStructure = false;

                                // Remove all documents that are not in sharepoint
                                var documentsToDelete = currentLibrary.Keys.Except(documentsInSharepoint.Keys).ToList();
                                if (documentsToDelete != null && documentsToDelete.Count != 0)
                                {
                                    logger?.LogInformation($"Detected {documentsToDelete.Count} elements to be removed");
                                    foreach (var documentKey in documentsToDelete)
                                    {
                                        currentLibrary[documentKey].ToBeDeleted = true;
                                    }                                                                      
                                    updateLibrariyStructure = true;
                                }

                                // Update all documents that are not in the previous library (documentStatusDictionary)
                                var documentsToUpdate = documentsInSharepoint.Keys.Except(currentLibrary.Keys).ToList();                         
                                var documentsWithDifferentHash = documentsInSharepoint
                                    .Where(d => currentLibrary.ContainsKey(d.Key) && currentLibrary[d.Key].Hash != d.Value.Hash)
                                    .Select(d => d.Key)
                                    .ToList();
                                documentsToUpdate.AddRange(documentsWithDifferentHash);

                                if (documentsToUpdate != null && documentsToUpdate.Count != 0)
                                {
                                    logger?.LogInformation($"Detected {documentsToUpdate.Count} elements to be added / updated");
                                   
                                    foreach (var documentKey in documentsToUpdate)
                                    {
                                        if (!currentLibrary.TryGetValue(documentKey, out EmbeddingRequest value))
                                        {
                                            value = documentsInSharepoint[documentKey];
                                            currentLibrary.Add(documentKey, value);
                                        }
                                        value.Hash = documentsInSharepoint[documentKey].Hash;
                                        value.ToBeUpdated = true;
                                    }                               
                                    updateLibrariyStructure = true;
                                }

                                if (updateLibrariyStructure)
                                {
                                    logger?.LogInformation($"Saving the new library estructure");
                                    if (library != null)
                                    {
                                        this.dataAccess.LibraryStructure.Delete(library.Id);
                                        await this.dataAccess.SaveChangesAsync();
                                    }
                                    await this.dataAccess.LibraryStructure.InsertAsync(new LibraryStructure
                                    {
                                        Id = Guid.NewGuid(),                                        
                                        LastUpdate = DateTime.Now,
                                        SiteUri = library.SiteUri,
                                        RootFolder = library.RootFolder,
                                        Documents = [.. currentLibrary.Values],
                                    });
                                    await this.dataAccess.SaveChangesAsync();
                                }
                                else
                                {
                                    logger?.LogInformation($"No changes found in the sharepoint library");
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            logger?.LogInformation($"Error processing library {library.SiteUri}: {ex.Message}");
                        }
                    };
                }
                logger?.LogInformation($"Finished the sync process with Sharepoint library");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException != null ? ex.InnerException.Message : "";
                logger?.LogError($"{ex.Message} :{innerException}");
            }
        }


        /// <summary>
        /// Builds the document status tree
        /// </summary>
        private async Task BuildDocumentStatus( LibraryStructure library, string currentPath, Folder folder, Dictionary<string, EmbeddingRequest> statusDictionary)
        {
            var currentNode = new EmbeddingRequest { Filename = folder.Name, Uri = $"{currentPath}/{folder.Name}", IsFolder = true };
            ClientRuntimeContext cc;
            try
            {
                // Create a tree node for the current folder          
                cc = folder.Context;     
                cc.Load(folder, f => f.Folders, f => f.Files);
                cc.ExecuteQuery();
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error processing folder {currentNode.Uri}: {ex.Message}");
                return;
            }

            // Recursively process subfolders
            foreach (var subfolder in folder.Folders)
            {
                await BuildDocumentStatus(library, currentNode.Uri, subfolder, statusDictionary);
            }

            // Process files in the current folder
            foreach (var file in folder.Files)
            {
                try
                {
                    var listItem = file.ListItemAllFields;
                    cc.Load(listItem);
                    cc.Load(file, f => f.ListId);

                    cc.ExecuteQuery();

                    // Create a tree node for the current file
                    if (file.Name.Contains(".aspx"))
                    {
                        logger?.LogInformation($"Ignoring {file.Name} because it's a page");
                    }
                    else
                    {
                        if (file.Name.Contains(".docx")
                            || file.Name.Contains(".pptx")
                            || file.Name.Contains(".xlsx")
                            || file.Name.Contains(".pdf"))
                        {
                            
                             //var sharepointSite = new Uri(;
                            var siteUri = new Uri(library.SiteUri);
                            var fileUri = new Uri($"https://{siteUri.Host}{file.ServerRelativeUrl}");
                         
                            //var uri = file.ServerRelativeUrl''
                            var fileNode = new EmbeddingRequest
                            {
                                Filename = file.Name,
                                Uri = Uri.EscapeUriString(fileUri.ToString()),
                                IsFolder = false,
                                SiteUri = library.SiteUri,
                                Folder = folder.Name, //$"{library.RootFolder}/{CleanupPath(currentNode.Uri)}",
                                Hash = string.Empty,                                
                                TimeLastModified = file.TimeLastModified,
                                Version = $"{file.MajorVersion}.{file.MinorVersion}",
                                GroupIds = await this.GetFileAccessPrincipals(library.SiteUri, file),
                                Year = string.Empty,
                                ToBeUpdated = false,
                                ToBeDeleted = false,
                            };
                            fileNode.Hash = GenerateHash(JsonConvert.SerializeObject(fileNode));                         
                            statusDictionary[fileNode.Uri] = fileNode;
                            logger?.LogInformation($"Added {currentNode.Uri}/{file.Name} to the folder and files collection");
                        }
                        else
                        {
                            logger?.LogInformation($"Ignoring {file.Name} because it's not a supported file type");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Error processing file {currentNode.Uri}/{file.Name}: {ex.Message}");
                }
            }
        }


        /// <summary>
        ///  Finds the year value on the librery metadata
        /// </summary>
        private string GetYearValue(Microsoft.SharePoint.Client.File file)
        {
            try
            {
                return file.ListItemAllFields[this.yearMetadataField].ToString();
            }
            catch
            {
                return string.Empty;
            }
        }


        /// <summary>
        /// Clean up the folder path
        /// </summary>
        private static string CleanupPath(string path)
        {
            var folder = WebUtility.HtmlDecode(path);
            var rootFolder = folder.Replace("%20", " ");
            folder = folder.Replace(rootFolder, "");
            folder = folder.Replace("//", "/");
            folder = folder.Replace("á", "a");
            folder = folder.Replace("é", "e");
            folder = folder.Replace("í", "i");
            folder = folder.Replace("ó", "o");
            folder = folder.Replace("ú", "u");
            folder = folder.Replace("ñ", "n");
            return folder;
        }

    
        /// <summary>
        /// Returns the groups that have access to the file
        /// </summary>
        private async Task<string[]> GetFileAccessPrincipals(string siteUri, Microsoft.SharePoint.Client.File file)
        {
            try
            {
                var query = $"_api/web/Lists('{file.ListId}')/GetItemById('{file.ListItemAllFields.Id}')/GetSharingInformation?$Expand=permissionsInformation";

                var apiRequest = new ApiRequest(ApiRequestType.SPORest, query)
                {
                    HttpMethod = HttpMethod.Post
                };

                var response = (await this.GetCorePnPContext(siteUri)).Web.ExecuteRequest(apiRequest);
                var resultado = JsonNode.Parse(response.Response);
                var groupPermissions = resultado!["d"]!["permissionsInformation"]!["principals"]!["results"];
                if (groupPermissions is JsonArray permissionArray)
                {
                    var guidExpression = @"\b\w{8}-(\w{4}-){3}\w{12}\b";
                    return permissionArray.Select(x =>
                    {
                        var loginName = x!["principal"]!["loginName"]!.ToString();
                        var loginNameParts = loginName.Split("|");
                        var tenantString = loginNameParts.ElementAtOrDefault(1);
                        var guidString = loginNameParts.ElementAtOrDefault(2);
                        if (tenantString == "tenant" && Regex.IsMatch(guidString, guidExpression))
                        {
                            return guidString;
                        }
                        return null;
                    }).Where(x => !string.IsNullOrEmpty(x)).ToArray();
                }
                else
                {
                    return [];
                }
            }
            catch(Exception ex)
            {
                logger?.LogError($"Unable to get permission form {siteUri}, for File: {file.Name}: {ex.Message}");
                return [];
            }

        }


        /// <summary>
        /// Generates a file hasg to 
        /// </summary>
        private static string GenerateHash(string inputString)
        {
            var inputBytes = Encoding.UTF8.GetBytes(inputString);
            var hashBytes = MD5.HashData(inputBytes);
            var stringBuilder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                stringBuilder.Append(hashBytes[i].ToString("x2"));
            }
            string md5Hash = stringBuilder.ToString();
            return md5Hash;
        }
    }
}
