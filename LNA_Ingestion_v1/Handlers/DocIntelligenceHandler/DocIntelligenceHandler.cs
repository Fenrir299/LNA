using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using Newtonsoft.Json;
using NLog;
using System.Text;
using System.Text.Json.Serialization;

namespace Custom.Ingestion
{
    public class DocIntelligenceHandler : IPipelineStepHandler
    {

        private readonly IPipelineOrchestrator _orchestrator;
        private readonly ILogger<DocIntelligenceHandler> _log;
        private DocumentAnalysisClient _docIntelClient;

        public DocIntelligenceHandler(
            string stepName,
            IPipelineOrchestrator orchestrator,
            //ILogger<DocIntelligenceHandler>? log = null)
            ILogger<DocIntelligenceHandler> logger = null)
        {
            this.StepName = stepName;
            this._orchestrator = orchestrator;
            _log = logger;
            //this._log = log ?? DefaultLogger<DocIntelligenceHandler>.Instance;

            AzureKeyCredential credential = new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_API_KEY") ?? "");
            this._docIntelClient = new DocumentAnalysisClient(new Uri(Environment.GetEnvironmentVariable("AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT") ?? ""), credential);
        }

        /// <inheritdoc />
        public string StepName { get; }

        /// <inheritdoc />
        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken)
        {
            try
            {
                _log.LogInformation("> Doc Intelligence : DEBUT ===============");
                _log.LogInformation("> Doc Intelligence : Pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

                foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
                {
                    if (uploadedFile.AlreadyProcessedBy(this))
                    {
                        _log.LogTrace("> Doc Intelligence : File {0} already processed by this handler", uploadedFile.Name);
                        continue;
                    }

                    var sourceFile = uploadedFile.Name;
                    var destFile = $"{uploadedFile.Name}.extract.txt";


                    BinaryData fileContent = await this._orchestrator.ReadFileAsync(pipeline, sourceFile, cancellationToken).ConfigureAwait(false);

                    var content = String.Empty;

                    //#pragma warning disable KMEXP00
                    switch (uploadedFile.MimeType)
                    {
                        case MimeTypes.Json:
                            content += fileContent;
                            break;
                        case MimeTypes.MsWordX:
                            content = new MsWordDecoderOP().DocToText(fileContent);
                            break;
                        case MimeTypes.MsWord:
                            content = new MsWordDecoderOP().DocToText(fileContent);
                            break;
                        case MimeTypes.MsPowerPoint:
                        case MimeTypes.MsPowerPointX:
                            content = new MsPowerPointDecoderOP().DocToText(fileContent, withSlideNumber: false, withEndOfSlideMarker: false, skipHiddenSlides: true);
                            break;
                        case MimeTypes.MsExcel:
                        case MimeTypes.MsExcelX:
                            content = new MsExcelDecoderOP().DocToText(fileContent);
                            break;
                        default:
                        case MimeTypes.Pdf:
                            _log.LogInformation("> Doc Intelligence : Send document to Azure Intelligence service...");

                            var result = await _docIntelClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", fileContent.ToStream());

                            foreach (var page in result.Value.Pages)
                            {
                                foreach (var line in page.Lines)
                                {
                                    content += $"{line.Content}\r\n";
                                }
                            }

                            break;
                    }
                    if (content.Length > 0)
                    {
                        // write extracted content to file
                        _log.LogInformation("> Doc Intelligence : Write extracted text to document...");

                        await this._orchestrator.WriteFileAsync(pipeline, destFile, new BinaryData(content), cancellationToken).ConfigureAwait(false);

                        var destFileDetails = new DataPipeline.GeneratedFileDetails
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            ParentId = uploadedFile.Id,
                            Name = destFile,
                            Size = content.Length,

                            MimeType = MimeTypes.PlainText,
                            ArtifactType = DataPipeline.ArtifactTypes.ExtractedText,
                            Tags = pipeline.Tags,
                        };

                        uploadedFile.GeneratedFiles.Add(destFile, destFileDetails);

                        _log.LogInformation($"> Doc Intelligence : Content extracted successfully in {destFile}...");
                    }

                    uploadedFile.MarkProcessedBy(this);
                }

                _log.LogInformation("> Doc Intelligence : FIN   ===============");

                return (ReturnType.Success, pipeline);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ERREUR INGESTION");
                return (ReturnType.Success, pipeline);
            }
        }
    }
}