using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GenAI.Common.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        //services.AddLogging(logging => logging.AddGenAiLogging());
    })
    .ConfigureLogging(logging =>
    {
        logging.AddGenAiLogging();
    })
    .Build();
#pragma warning restore AZFW0014 
host.Run();
