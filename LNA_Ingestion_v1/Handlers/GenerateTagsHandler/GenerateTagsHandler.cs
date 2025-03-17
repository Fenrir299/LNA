using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Extensions;
using Microsoft.KernelMemory.Pipeline;
using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace Custom.Ingestion
{
    public class GenerateTagsHandler : IPipelineStepHandler
    {
        private const int MinLength = 50;
        private const int SummaryMaxTokens = 2040;
        private const int OverlappingTokens = 200;
        private const int MaxTokensPerParagraph = SummaryMaxTokens / 2;
        private const int MaxTokensPerLine = 300;

        private readonly IPipelineOrchestrator _orchestrator;
        private readonly ILogger<GenerateTagsHandler> _log;
        private readonly string _genTagsPrompt;

        public GenerateTagsHandler(
            string stepName,
            IPipelineOrchestrator orchestrator,
            ILogger<GenerateTagsHandler>? logger = null)
        {
            this.StepName = stepName;
            this._orchestrator = orchestrator;
            //this._log = log ?? DefaultLogger<GenerateTagsHandler>.Instance;
            _log = logger;

            //string handlersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Handlers");
            //string handlersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Handlers");
            //string filePath = Path.Combine(handlersDirectory, "GenerateTagsHandler\\generate-tags.txt");
            //_genTagsPrompt = File.ReadAllText(filePath);
            _genTagsPrompt = "Extrait uniquement le nom du contrat. Pas de phrases ou d'autres informations.\nLa réponse ne doit comprendre uniquement le nom du contrat et rien d'autres.\nRésumé : {{$input}}";
        }

        /// <inheritdoc />
        public string StepName { get; }

        /// <inheritdoc />
        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
        {
            _log.LogInformation("> GenerateTagsHandler : DEBUT ===============");
            _log.LogInformation("> GenerateTagsHandler :Generating tags list, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

            foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
            {
                // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
                Dictionary<string, DataPipeline.GeneratedFileDetails> tagsListFiles = new();

                foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
                {
                    var file = generatedFile.Value;

                    if (file.AlreadyProcessedBy(this))
                    {
                        _log.LogTrace("> GenerateTagsHandler : File {0} already processed by this handler", file.Name);
                        continue;
                    }

                    // Only process the summarized content
                    if (file.ArtifactType == DataPipeline.ArtifactTypes.ExtractedText)
                    {
                        switch (file.MimeType)
                        {
                            case MimeTypes.PlainText:
                            case MimeTypes.MarkDown:
                                _log.LogInformation("> GenerateTagsHandler :Tagging text file {0}", file.Name);
                                string content = (await _orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false)).ToString();
                                (string tags, bool success) = await TagFilesAsync(content).ConfigureAwait(false);
                                if (success)
                                {
                                    var tagsList = new List<string>();
                                    tags.Split(',').Distinct().ToList().ForEach(tag => tagsList.Add(tag.Trim()));
                                    pipeline.Tags.Add(new KeyValuePair<string, List<string?>>("Contrat", tagsList.Distinct().ToList())); // Nom tags

                                    _log.LogInformation("> GenerateTagsHandler : Creation du Tag - Contrat ");

                                    // for debug purpose, write tags in file also
                                    var tagsData = new BinaryData(tags);
                                    var destFile = uploadedFile.GetHandlerOutputFileName(this);
                                    await _orchestrator.WriteFileAsync(pipeline, destFile, tagsData, cancellationToken).ConfigureAwait(false);

                                    tagsListFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
                                    {
                                        Id = Guid.NewGuid().ToString("N"),
                                        ParentId = uploadedFile.Id,
                                        Name = destFile,
                                        Size = tags.Length,
                                        MimeType = MimeTypes.PlainText,
                                        ArtifactType = DataPipeline.ArtifactTypes.SyntheticData,
                                        ContentSHA256 = Convert.ToHexString(SHA256.HashData(tagsData.ToMemory().Span)).ToLowerInvariant()
                                    });
                                }

                                break;

                            default:
                                _log.LogWarning("> GenerateTagsHandler : File {0} cannot be summarized, type not supported", file.Name);
                                continue;
                        }
                    }

                    file.MarkProcessedBy(this);
                }
            }
            _log.LogInformation("> GenerateTagsHandler : FIN   ===============");
            return (ReturnType.Success, pipeline);
        }


        private async Task<(string tags, bool skip)> TagFilesAsync(string content)
        {
            GPT4oTokenizer gtok = new GPT4oTokenizer();
            int contentLength = gtok.CountTokens(content);
            if (contentLength < MinLength)
            {
                _log.LogDebug("> GenerateTagsHandler : Content too short to generate tags, {0} tokens", contentLength);
                return (content, false);
            }

            ITextGenerator textGenerator = _orchestrator.GetTextGenerator();

            // Summarize List at least once
            var done = false;

            var newContent = new StringBuilder();
            _log.LogTrace("> GenerateTagsHandler : Generating tags");

            var filledPrompt = _genTagsPrompt.Replace("{{$input}}", content, StringComparison.OrdinalIgnoreCase);
            await foreach (string token in textGenerator.GenerateTextAsync(filledPrompt, new TextGenerationOptions()).ConfigureAwait(false))
            {
                newContent.Append(token);
            }

            return (newContent.ToString(), true);
        }
    }
}