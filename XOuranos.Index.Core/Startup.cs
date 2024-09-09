using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using XOuranos.Index.Core.Client;
using XOuranos.Index.Core.Crypto;
using XOuranos.Index.Core.Extensions;
using XOuranos.Index.Core.Handlers;
using XOuranos.Index.Core.Operations;
using XOuranos.Index.Core.Operations.Types;
using XOuranos.Index.Core.Paging;
using XOuranos.Index.Core.Settings;
using XOuranos.Index.Core.Storage;
using XOuranos.Index.Core.Storage.Mongo;
using XOuranos.Index.Core.Sync;
using XOuranos.Index.Core.Sync.SyncTasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace XOuranos.Index.Core
{
   public class Startup
   {
      public static void AddIndexerServices(IServiceCollection services, IConfiguration configuration)
      {
         services.Configure<ChainSettings>(configuration.GetSection("Chain"));
         services.Configure<NetworkSettings>(configuration.GetSection("Network"));
         services.Configure<IndexerSettings>(configuration.GetSection("Indexer"));
         services.Configure<InsightSettings>(configuration.GetSection("Insight"));

         IndexerSettings indexer = new IndexerSettings();

         configuration.GetSection("Indexer").Bind(indexer);

         switch (indexer.DbType)
         {
            case "MongoDb":
               services.AddMongoDatabase();
               break;
            default: throw new InvalidOperationException();
         }

         // services.AddSingleton<QueryHandler>();
         services.AddSingleton<StatsHandler>();
         services.AddSingleton<CommandHandler>();

         services.AddTransient<SyncServer>();
         services.AddSingleton<SyncConnection>();
         services.AddSingleton<ISyncOperations, SyncOperations>();
         services.AddSingleton<IPagingHelper, PagingHelper>();
         services.AddScoped<Runner>();

         services.AddSingleton<GlobalState>();
         services.AddSingleton<IScriptInterpreter, ScriptToAddressParser>();


         services.AddScoped<TaskRunner, MempoolPuller>();
         services.AddScoped<TaskRunner, Notifier>();
         services.AddScoped<TaskRunner, StatsSyncer>(); // Update peer information every 5 minute.

         services.AddScoped<TaskRunner, BlockPuller>();
         services.AddScoped<TaskRunner, BlockStore>();
         services.AddScoped<TaskStarter, BlockStartup>();

         // TODO: Verify that it is OK we add this to shared Startup for XOuranos and Cirrus.
         services.AddScoped<TaskRunner, HistoryComputer>();
         services.AddSingleton<IComputeHistoryQueue, ComputeHistoryQueue>();

         services.AddResponseCompression();
         services.AddMemoryCache();
         services.AddHostedService<SyncServer>();

         services.AddControllers(options =>
         {
            options.ModelBinderProviders.Insert(0, new DateTimeModelBinderProvider());
            options.Conventions.Add(new ActionHidingConvention());
         }).AddJsonOptions(options =>
         {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,
               allowIntegerValues: false));
         }).AddNewtonsoftJson(options =>
         {
            options.SerializerSettings.FloatFormatHandling = FloatFormatHandling.DefaultValue;
            options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore; // Don't include null fields.
         });

         services.AddSwaggerGen(
            options =>
            {
               string assemblyVersion = typeof(Startup).Assembly.GetName().Version.ToString();

               options.SwaggerDoc("indexer",
                  new OpenApiInfo
                  {
                     Title = "XOuranos Indexer API",
                     Version = assemblyVersion,
                     Description = "Blockchain index database that can be used for blockchain based software and services.",
                     Contact = new OpenApiContact
                     {
                        Name = "XOuranos",
                        Url = new Uri("https://www.xouranos.com/")
                     }
                  });

               // integrate xml comments
               if (File.Exists(XmlCommentsFilePath))
               {
                  options.IncludeXmlComments(XmlCommentsFilePath);
               }

               options.EnableAnnotations();
            });

         services.AddSwaggerGenNewtonsoftSupport(); // explicit opt-in - needs to be placed after AddSwaggerGen()

         services.AddCors(o => o.AddPolicy("IndexerPolicy", builder =>
         {
            builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
         }));


         services.AddSingleton<ICryptoClientFactory, CryptoClientFactory>();
         services.AddSingleton<ISyncBlockTransactionOperationBuilder, SyncBlockTransactionOperationBuilder>();

         // TODO: Verify that it is OK we add this to shared Startup for XOuranos and Cirrus.
         services.AddTransient<IBlockRewindOperation, BlockRewindOperation>();
      }

      public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
      {
         app.UseExceptionHandler("/error");

         // Enable Cors
         app.UseCors("IndexerPolicy");

         app.UseResponseCompression();

         //app.UseMvc();

         app.UseDefaultFiles();

         app.UseStaticFiles();

         app.UseRouting();

         app.UseSwagger(c =>
         {
            c.RouteTemplate = "docs/{documentName}/openapi.json";
         });

         app.UseSwaggerUI(c =>
         {
            c.RoutePrefix = "docs";
            c.SwaggerEndpoint("/docs/indexer/openapi.json", "XOuranos Indexer API");
         });

         app.UseEndpoints(endpoints =>
         {
            endpoints.MapControllers();
         });
      }

      private static string XmlCommentsFilePath
      {
         get
         {
            string basePath = PlatformServices.Default.Application.ApplicationBasePath;
            string fileName = typeof(Startup).GetTypeInfo().Assembly.GetName().Name + ".xml";
            return Path.Combine(basePath, fileName);
         }
      }

      /// <summary>
      /// Hide Stratis related endpoints in Swagger shown due to using Nuget packages
      /// in WebApi project for serialization.
      /// </summary>
      public class ActionHidingConvention : IActionModelConvention
      {
         public void Apply(ActionModel action)
         {
            // Replace with any logic you want
            if (!action.Controller.DisplayName.Contains("XOuranos.Index"))
            {
               action.ApiExplorer.IsVisible = false;
            }
         }
      }
   }
}