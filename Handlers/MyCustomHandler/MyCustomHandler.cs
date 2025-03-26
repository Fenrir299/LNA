// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Diagnostics;
using Microsoft.KernelMemory.Pipeline;
using NLog;
using System.Text;

namespace Custom.Ingestion
{
    public class MyCustomHandler : IHostedService, IPipelineStepHandler
    {
        private readonly IPipelineOrchestrator _orchestrator;
        private readonly ILogger<MyCustomHandler> _log;

        public MyCustomHandler(
            string stepName,
            IPipelineOrchestrator orchestrator,
            //ILoggerFactory? loggerFactory = null)
            ILogger<MyCustomHandler> logger = null)
        {
            this.StepName = stepName;
            this._orchestrator = orchestrator;
            //this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TestH>();
            _log = logger;

            this._log.LogInformation("Instantiating handler {0}...", this.GetType().FullName);
        }

        /// <inheritdoc />
        public string StepName { get; }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this._log.LogInformation("Starting handler {0}...", this.GetType().FullName);
            return this._orchestrator.AddHandlerAsync(this, cancellationToken);
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            this._log.LogInformation("Stopping handler {0}...", this.GetType().FullName);
            return this._orchestrator.StopAllPipelinesAsync();
        }

        /// <inheritdoc />
        public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
        {
            _log.LogInformation("> MyCustomHandler : DEBUT ===============");
            /* ... your custom ...
             * ... handler ...
             * ... business logic ... */

            foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
            {
                foreach (var generatedFile in uploadedFile.GeneratedFiles)
                {
                    _log.LogInformation("> MyCustomHandler : Running handler {0}...", this.GetType().FullName);
                }
            }

            // Remove this - here only to avoid build errors
            await Task.Delay(0, cancellationToken).ConfigureAwait(false);

            _log.LogInformation("> MyCustomHandler : FIN ===============");
            return (ReturnType.Success, pipeline);
        }
    }
}

// Copyright (c) Microsoft. All rights reserved.

//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.KernelMemory.AI;
//using Microsoft.KernelMemory.Diagnostics;
//using Microsoft.KernelMemory.Pipeline;

//public class TestH : IHostedService, IPipelineStepHandler
//{
//    private readonly IPipelineOrchestrator _orchestrator;
//    private readonly ILogger<TestH> _log;

//    public TestH(
//        string stepName,
//        IPipelineOrchestrator orchestrator,
//        ILoggerFactory? loggerFactory = null)
//    {
//        this.StepName = stepName;
//        this._orchestrator = orchestrator;
//        this._log = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<TestH>();

//        this._log.LogInformation("Instantiating handler {0}...", this.GetType().FullName);
//    }

//    /// <inheritdoc />
//    public string StepName { get; }

//    /// <inheritdoc />
//    public Task StartAsync(CancellationToken cancellationToken = default)
//    {
//        this._log.LogInformation("Starting handler {0}...", this.GetType().FullName);
//        return this._orchestrator.AddHandlerAsync(this, cancellationToken);
//    }

//    /// <inheritdoc />
//    public Task StopAsync(CancellationToken cancellationToken = default)
//    {
//        this._log.LogInformation("Stopping handler {0}...", this.GetType().FullName);
//        return this._orchestrator.StopAllPipelinesAsync();
//    }

//    /// <inheritdoc />
//    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(DataPipeline pipeline, CancellationToken cancellationToken = default)
//    {
//        /* ... your custom ...
//         * ... handler ...
//         * ... business logic ... */

//        this._log.LogInformation("Running handler {0}...", this.GetType().FullName);

//        // Remove this - here only to avoid build errors
//        await Task.Delay(0, cancellationToken).ConfigureAwait(false);

//        return (ReturnType.Success, pipeline);
//    }
//}