using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Backend.Common.Interfaces;
using Backend.Embedding.Logic;

namespace Backend.Embedding.Service
{
    /// <summary>
    /// Azure Function entry point
    /// </summary>
    public static class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
              .ConfigureFunctionsWorkerDefaults(builder =>
              {
                  builder.UseNewtonsoftJson();
              })
              .ConfigureOpenApi()
                .ConfigureAppConfiguration(builder =>
                {
                    var connectionString = Environment.GetEnvironmentVariable("AppConfiguration");
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        builder.AddAzureAppConfiguration(connectionString);
                    }
                })
              .ConfigureAppConfiguration(config => config
                  .SetBasePath(Environment.CurrentDirectory)
                   .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                  .AddEnvironmentVariables())
              .ConfigureServices(services =>
              {
                  services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
                  {
                      var options = new OpenApiConfigurationOptions()
                      {
                          Info = new OpenApiInfo()
                          {
                              Version = "1.0.0",
                              Title = "AI Embeddings API",
                              Description = "AI Embeddings API",
                              Contact = new OpenApiContact()
                              {
                                  Name = "Soporte",
                                  Email = "soporte@bancard.py",
                              },                            
                          },
#if DEBUG
                          ForceHttps = false,
                          ForceHttp = false,
#else
                            ForceHttps = true,
                          ForceHttp = false,
#endif
                          Servers = new System.Collections.Generic.List<OpenApiServer>()
                            {
                                new OpenApiServer() { Url = "dasd" },
                            }
                      };

                      return options;
                  });
                  services.AddScoped<ISharePointSyncLogic, SharePointSyncLogic>();
                  services.AddScoped<IEmbeddingsBusinessLogic, EmbeddingsBusinessLogic>();                  
                  services.AddScoped<IDataAccess, DataAccess.DataAccess>();
              })
              .Build();

            host.Run();
        }
    }
}
