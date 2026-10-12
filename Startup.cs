using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.Http.Features;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Stores;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using BackEnd.Entities;
using Microsoft.OpenApi.Models;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;

namespace BackEnd
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
            Console.WriteLine("Startup constructor called.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                Console.WriteLine("Starting ConfigureServices...");

               var credential = new DefaultAzureCredential();

                var secretClient = new SecretClient(
                    new Uri("https://cw-keyvault-manu-2026.vault.azure.net/"),
                    credential);

                string cosmosDbConnectionString =
                    secretClient.GetSecret("cosmosDbConnectionString").Value.Value;

                string blobConnectionString =
                    secretClient.GetSecret("blobConnectionString").Value.Value;

                if (string.IsNullOrEmpty(cosmosDbConnectionString) || string.IsNullOrEmpty(blobConnectionString))
                {
                    throw new Exception("Required connection strings are missing.");
                }

                var cosmosClientOptions = new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct,
                    MaxRequestsPerTcpConnection = 10,
                    MaxTcpConnectionsPerEndpoint = 10
                };

                var cosmosClient = new CosmosClient(cosmosDbConnectionString, cosmosClientOptions);
                services.AddSingleton(cosmosClient);
                services.AddScoped<CosmosDbContext>();
                services.AddSingleton(new BlobServiceClient(blobConnectionString));

                services.Configure<FormOptions>(options =>
                {
                    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
                });

                services.Configure<IISServerOptions>(options =>
                {
                    options.MaxRequestBodySize = 500 * 1024 * 1024;
                });

                services.Configure<KestrelServerOptions>(options =>
                {
                    options.Limits.MaxRequestBodySize = 500 * 1024 * 1024;
                    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
                    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
                });

                services.AddCors(options =>
                {
                    options.AddPolicy("AllowSpecificOrigin", builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyHeader()
                               .AllowAnyMethod();
                    });
                });

                services.AddControllers();

                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                });

                Console.WriteLine("ConfigureServices completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConfigureServices: {ex}");
                throw;
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            try
            {
                logger.LogInformation("Starting Configure...");



                //if (env.IsDevelopment())
                //{


                app.UseSwagger();
                app.UseDeveloperExceptionPage();
   
                    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));
                //}

                app.UseHttpsRedirection();


                //   app.UseMiddleware<AllowAllCorsMiddleware>();


                app.UseCors("AllowSpecificOrigin");

                app.UseRouting();

                app.UseTus(httpContext => new DefaultTusConfiguration
                {
                    Store = new TusDiskStore(Path.Combine(env.ContentRootPath, "uploads")),
                    UrlPath = "/files",
                    MaxAllowedUploadSizeInBytes = 500 * 1024 * 1024,
                    Events = new Events
                    {
                        OnFileCompleteAsync = async ctx =>
                        {
                            var fileId = ctx.FileId;
                            var filePath = Path.Combine(env.ContentRootPath, "uploads", fileId);
                            logger.LogInformation($"File {fileId} uploaded to {filePath}");
                        }
                    }
                });

              //  app.UseMiddleware<SkipAuthorizationMiddleware>();

                app.Use(async (context, next) =>
                {
                    logger.LogInformation("Request: {Method} {Path}", context.Request.Method, context.Request.Path);
                    await next.Invoke();
                    logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
                });

                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

                logger.LogInformation("Application configured successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Configuration error: {ex}");
                throw;
            }
        }
    }
}
