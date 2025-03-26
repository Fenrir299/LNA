// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.DataFormats.Text;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Extensions;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.Prompts;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.KernelMemory.Handlers
{
    public class ExtractCustomHandler2 : IPipelineStepHandler
    {
        private const int MinLength = 50;

        private readonly IPipelineOrchestrator _orchestrator;
        private readonly ILogger<ExtractCustomHandler2> _log;
        private readonly string _summarizationPrompt;

        public string StepName { get; }

        public ExtractCustomHandler2(
            string stepName,
            IPipelineOrchestrator orchestrator,
            IPromptProvider? promptProvider = null,
            ILogger<ExtractCustomHandler2>? logger = null)
        {
            StepName = stepName;
            _orchestrator = orchestrator;
            //string handlersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Handlers");
            //string filePath = Path.Combine(handlersDirectory, "SummarizationUcanssHandler\\summarization.txt");

            _summarizationPrompt = "Extrait uniquement le thème du contenu. Pas de phrases ou d'autres informations.\nLa réponse ne doit comprendre uniquement le thèmeet rien d'autres.\nRésumé : {{$input}}";

            //_log = log ?? DefaultLogger<ExtractCustomHandler2>.Instance;
            _log = logger;

            _log.LogInformation("Handler '{0}' ready", stepName);
        }

        /// <inheritdoc />
        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
        {
            _log.LogDebug("Generating summary, pipeline '{0}/{1}'", pipeline.Index, pipeline.DocumentId);

            foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
            {
                try
                {
                    if (uploadedFile.MimeType == MimeTypes.Pdf)
                    {
                        // Track new files being generated (cannot edit originalFile.GeneratedFiles while looping it)
                        Dictionary<string, DataPipeline.GeneratedFileDetails> summaryFiles = new();

                        foreach (KeyValuePair<string, DataPipeline.GeneratedFileDetails> generatedFile in uploadedFile.GeneratedFiles)
                        {
                            var file = generatedFile.Value;

                            if (file.AlreadyProcessedBy(this))
                            {
                                _log.LogTrace("File {0} already processed by this handler", file.Name);
                                continue;
                            }

                            // Summarize only the original content
                            if (file.ArtifactType != DataPipeline.ArtifactTypes.ExtractedText)
                            {
                                _log.LogTrace("Skipping file {0}", file.Name);
                                continue;
                            }

                            switch (file.MimeType)
                            {
                                case MimeTypes.PlainText:
                                case MimeTypes.MarkDown:
                                    _log.LogDebug("Summarizing text file {0}", file.Name);
                                    string content = (await _orchestrator.ReadFileAsync(pipeline, file.Name, cancellationToken).ConfigureAwait(false)).ToString();
                                    (string summary, bool success) = await SummarizeAsync(content).ConfigureAwait(false);
                                    if (success)
                                    {
                                        var summaryData = new BinaryData(summary);
                                        var destFile = uploadedFile.GetHandlerOutputFileName(this);
                                        await _orchestrator.WriteFileAsync(pipeline, destFile, summaryData, cancellationToken).ConfigureAwait(false);

                                        summaryFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
                                        {
                                            Id = Guid.NewGuid().ToString("N"),
                                            ParentId = uploadedFile.Id,
                                            Name = destFile,
                                            Size = summary.Length,
                                            MimeType = MimeTypes.PlainText,
                                            ArtifactType = DataPipeline.ArtifactTypes.SyntheticData,
                                            Tags = pipeline.Tags.Clone().AddSyntheticTag(Constants.TagsSyntheticSummary),
                                            ContentSHA256 = Convert.ToHexString(SHA256.HashData(summaryData.ToMemory().Span)).ToLowerInvariant()
                                        });
                                    }

                                    break;

                                default:
                                    _log.LogWarning("File {0} cannot be summarized, type not supported", file.Name);
                                    continue;
                            }

                            file.MarkProcessedBy(this);
                        }

                        // Add new files to pipeline status
                        foreach (var file in summaryFiles)
                        {
                            file.Value.MarkProcessedBy(this);
                            uploadedFile.GeneratedFiles.Add(file.Key, file.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, ex.Message + " / " + uploadedFile.Name);
                }
            }

            return (ReturnType.Success, pipeline);
        }

        private async Task<(string summary, bool skip)> SummarizeAsync(string content)
        {
            ITextGenerator textGenerator = _orchestrator.GetTextGenerator();
            var newContent = new StringBuilder();
            var d = new TextGenerationOptions();
            d.Temperature = 0;
            d.MaxTokens = 3000;

            _log.LogTrace("Summarization");
            var filledPrompt = _summarizationPrompt.Replace("{{$input}}", content, StringComparison.OrdinalIgnoreCase);
            await foreach (string token in textGenerator.GenerateTextAsync(filledPrompt, new TextGenerationOptions()).ConfigureAwait(false))
            {
                newContent.Append(token);
            }
            return (newContent.ToString().Replace("Résumé: ", ""), true);
        }
    }
}
