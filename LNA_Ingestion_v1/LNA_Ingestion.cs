using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.IO.Compression;
using Microsoft.KernelMemory;
using Custom.Ingestion;
using Microsoft.KernelMemory.Configuration;
using Microsoft.KernelMemory.Handlers;
using System.Reflection.Metadata;
using Microsoft.KernelMemory.DataFormats;

namespace LNA_Ingestion_v1
{
    public class LNA_Ingestion
    {
        private readonly ILogger<LNA_Ingestion> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public LNA_Ingestion(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LNA_Ingestion>();
            _loggerFactory = loggerFactory; 
        }

        [Function(nameof(LNA_Ingestion))]

        // full-doc-lna-input A VOIR
        public async Task Run([BlobTrigger("doc-lna-input/{name}", Connection = "STORAGE_ACCOUNT_CONNECTION_STRING")] byte[] blob, string name, Uri uri)
        {
            // Coonversion des fcihiers word en PDF
            if (name.Split(".").Last() == "doc" || name.Split(".").Last() == "docx")
            { 
            }
            
            try
            {
                _logger.LogInformation($"Blob trigger function Processed blob \n Name:{name}");

                var memoryBuilder = new KernelMemoryBuilder()
                    .WithAzureOpenAITextGeneration(new AzureOpenAIConfig
                    {
                        APIKey = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_API_KEY") ?? "",
                        APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
                        Endpoint = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_ENDPOINT") ?? "",
                        Deployment = Environment.GetEnvironmentVariable("CHAT_DEPLOYMENT_MODEL") ?? "",
                        Auth = AzureOpenAIConfig.AuthTypes.APIKey
                    })
                    .WithAzureOpenAITextEmbeddingGeneration(new AzureOpenAIConfig
                    {
                        APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                        Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                        Endpoint = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_ENDPOINT") ?? "",
                        APIKey = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_API_KEY") ?? "",
                        Deployment = Environment.GetEnvironmentVariable("TEXT_DEPLOYMENT_EMBEDDING_MODEL") ?? "",
                    })
                    .WithAzureAISearchMemoryDb(new AzureAISearchConfig
                    {
                        Auth = AzureAISearchConfig.AuthTypes.APIKey,
                        Endpoint = Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_ENDPOINT") ?? "",
                        APIKey = Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_API_KEY") ?? "",
                    })
                    .WithAzureAIDocIntel(new AzureAIDocIntelConfig
                    {
                        Auth = AzureAIDocIntelConfig.AuthTypes.APIKey,
                        Endpoint = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT") ?? "",
                        APIKey = Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_API_KEY") ?? "",
                    })
                    .WithAzureBlobsDocumentStorage(new AzureBlobsConfig
                    {
                        Auth = AzureBlobsConfig.AuthTypes.ConnectionString,
                        ConnectionString = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_CONNECTION_STRING") ?? "",
                        Container = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_DATA_PROCESSED_CONTAINER_NAME") ?? "",
                    });


                memoryBuilder.Build();
                var orchestrator = memoryBuilder.GetOrchestrator();

                var partitionOptions = new TextPartitioningOptions
                {
                    MaxTokensPerLine = 20,
                    MaxTokensPerParagraph = 500,
                    OverlappingTokens = 100
                };

                await orchestrator.AddHandlerAsync(new DocIntelligenceHandler(ConstantHandlers.DocumentIntelligence, orchestrator, _loggerFactory.CreateLogger<DocIntelligenceHandler>()));
                await orchestrator.AddHandlerAsync(new TextPartitioningHandler(ConstantHandlers.TextPartitionner, orchestrator, partitionOptions));
                await orchestrator.AddHandlerAsync(new GenerateEmbeddingsHandler(ConstantHandlers.GenerateEmbedding, orchestrator));
                //await orchestrator.AddHandlerAsync(new SaveRecordsHandler(ConstantHandlers.SaveRecord, orchestrator));
                await orchestrator.AddHandlerAsync(new SaveRecordsCustomHandler(ConstantHandlers.SaveRecord, orchestrator, null, _loggerFactory.CreateLogger<SaveRecordsCustomHandler>()));

                //(ConstantHandlers.SaveRecordOP, uri, orchestrator));

                _logger.LogInformation("=== DEB pipeline");
                var pipeline = orchestrator.PrepareNewDocumentUpload(index: Environment.GetEnvironmentVariable("INDEX_NAME") ?? ""
                    , documentId: name.Replace(".", ""), new TagCollection { { "Theme", "Book RH" } })
                    .AddUploadFile("file", name, blob)
                    .Then(ConstantHandlers.DocumentIntelligence)
                    .Then(ConstantHandlers.TextPartitionner)
                    .Then(ConstantHandlers.GenerateEmbedding)
                    .Then(ConstantHandlers.SaveRecord)
                    .Build();

                await orchestrator.RunPipelineAsync(pipeline);

                _logger.LogInformation("=== FIN pipeline");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERREUR INGESTION" + ex.Message);
            }
        }


        public class ConstantHandlers
        {
            public const string DocumentIntelligence = "document-intelligence";
            public const string TextPartitionner = "text-partitionner";
            public const string GenerateEmbedding = "generate-embedding";
            public const string SaveRecord = "save-record";
        }
    }
}
