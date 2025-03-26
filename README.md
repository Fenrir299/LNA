# LNA_Ingestion - Document Ingestion Pipeline with Azure and AI

## Overview

LNA_Ingestion is a document ingestion solution based on Azure Functions that transforms raw documents (PDF, Word, Excel, PowerPoint) into structured and enriched data that can be used by generative AI applications.

The pipeline performs the following operations:

1. Capture documents uploaded to an Azure Blob container
2. Extract text using Azure Document Intelligence
3. Segment text into AI-optimized partitions
4. Generate vector embeddings with Azure OpenAI
5. Automatic thematic classification
6. Indexing in Azure AI Search with semantic search configuration

## Architecture

![Architecture](https://via.placeholder.com/800x400?text=Architecture+LNA+Ingestion)

### Components

- **Azure Functions**: Serverless service triggering the ingestion pipeline for each document addition
- **Azure Blob Storage**: Storage for source documents and transformed files
- **Azure Document Intelligence**: Text extraction service from structured documents
- **Azure OpenAI**: AI service for vectorization and semantic analysis
- **Azure AI Search**: Vector indexing engine with semantic search capabilities
- **Microsoft Kernel Memory**: Framework facilitating data manipulation in the pipeline

## Project Structure

The project is divided into two main parts:

### LNA_Ingestion_v1

Main application containing:

- `LNA_Ingestion.cs`: Entry point and pipeline configuration
- `Handlers/`: Document processing components:
  - `DocIntelligenceHandler`: Text extraction
  - `TextPartitioningHandler`: Text segmentation
  - `GenerateEmbeddingsHandler`: Text vectorization
  - `SaveRecordsCustomHandler`: Azure AI Search storage
  - `ExtractCustomHandler`: Theme extraction
  - `GenerateTagsHandler`: Thematic tag generation

### GenAI.Common.Logging

Shared logging library:

- Centralized log configuration
- Support for log storage in Azure Blob Storage
- Standardized log message formatting

## Configuration

### Prerequisites

- .NET 8.0 SDK
- Azure account with services:
  - Azure Functions
  - Azure Blob Storage
  - Azure OpenAI
  - Azure Document Intelligence
  - Azure AI Search

### Environment Variables

The project requires the following environment variables in `local.settings.json` or in the Azure Functions application configuration:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "STORAGE_ACCOUNT_CONNECTION_STRING": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "STORAGE_ACCOUNT_DATA_PROCESSED_CONTAINER_NAME": "doc-lna-processed",
    "AZURE_OPEN_AI_API_KEY": "your-api-key",
    "AZURE_OPEN_AI_ENDPOINT": "https://your-service.openai.azure.com/",
    "CHAT_DEPLOYMENT_MODEL": "your-gpt-deployment",
    "TEXT_DEPLOYMENT_EMBEDDING_MODEL": "your-embedding-deployment",
    "AZURE_AI_SEARCH_ENDPOINT": "https://your-search-service.search.windows.net",
    "AZURE_AI_SEARCH_API_KEY": "your-api-key",
    "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT": "https://your-service.cognitiveservices.azure.com/",
    "AZURE_DOCUMENT_INTELLIGENCE_API_KEY": "your-api-key",
    "INDEX_NAME": "lna-index-search"
  }
}
```

## Deployment

### Local Deployment

1. Clone the repository
2. Create a `local.settings.json` file with required environment variables
3. Run `func start` to start the project locally

### Azure Deployment

1. Create a new Azure Function App
2. Configure environment variables in the Configuration section
3. Deploy the project using one of the following methods:
   - Visual Studio: Right-click on project > Publish
   - Azure CLI: `func azure functionapp publish <your-app-name>`
   - GitHub Actions: Configure a CI/CD workflow

## Usage

Once deployed, the service will automatically process any document uploaded to the specified blob container:

1. Upload a document (PDF, Word, Excel, PowerPoint) to the `doc-lna-input` container
2. The function triggers automatically and processes the document
3. Extracted and vectorized data is stored in Azure AI Search
4. Detailed logs are available in the `doc-lna-log` container

## Customization

### Adding a New Document Format

Modify `DocIntelligenceHandler.cs` to add support for a new format.

### Modifying AI Prompts

The prompts used for thematic extraction are located in:

- `ExtractCustomHandler.cs`: `_summarizationPrompt`
- `GenerateTagsHandler.cs`: `_genTagsPrompt`

## References

- [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory)
- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
- [Azure OpenAI Service](https://azure.microsoft.com/services/cognitive-services/openai-service/)
- [Azure AI Search](https://azure.microsoft.com/services/search/)
