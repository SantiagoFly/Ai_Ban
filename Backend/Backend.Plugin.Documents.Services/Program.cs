using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Backend.Common.Interfaces;
using Backend.Common.Middleware;
using DocumentSearch.BusinessLogic;
using Backend.Common.Providers;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;

namespace Backend.Plugin.Documents.Services
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
                  builder.UseMiddleware<BearerTokenMiddleware>();
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
                                  OpenApiVersion = OpenApiVersionType.V3,
                                  Info = new OpenApiInfo()
                                  {
                                      Version = "1.0.0",
                                      Title = "Documents query AI Plugin",
                                      Description = "Documents query AI Plugin",
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
                              new OpenApiServer() { Url = "asfa" },
                          }
                              };

                              return options;
                          });
                    services.AddScoped<ISessionProvider, SessionProvider>();
                    services.AddScoped<IQueryDocumentsPluginLogic, QueryDocumentsPluginLogic>();
                    services.AddScoped<IDataAccess, DataAccess.DataAccess>();
              })
              .Build();

            host.Run();
        }
    }
}
