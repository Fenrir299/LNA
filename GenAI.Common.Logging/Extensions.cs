using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using GenAI.Common.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GenAI.Common.Logging
{
    public static class Extensions
    {
        //TODO : Use appsettings.json for configuration, to integrate better with the .NET Core configuration system, we would be able to use environment variables, user secrets, etc.

        /// <summary>
        /// Add GenAI logging using the "nlog.config"
        /// </summary>
        /// <param name="loggingBuilder">Logging builder</param>
        /// <remarks>Config file should respect the "nlog.config" format</remarks>
        /// <returns>Logging builder</returns>
        public static ILoggingBuilder AddGenAiLogging(this ILoggingBuilder loggingBuilder)
        {
            {
                return AddGenAiLogging(loggingBuilder, "nlog.config");
            }
        }

        /// <summary>
        /// Add GenAI logging for the specified environment
        /// </summary>
        /// <param name="loggingBuilder">Logging builder</param>
        /// <param name="environmentName">Environment name</param>
        /// <remarks>Config file should respect the "nlog.{environmentName}.config" format</remarks>
        /// <returns>Logging builder</returns>
        public static ILoggingBuilder AddGenAiLoggingForEnvironment(this ILoggingBuilder loggingBuilder, string environmentName)
        {
            return AddGenAiLogging(loggingBuilder, $"nlog.{environmentName}.config");
        }

        /// <summary>
        /// Add GenAI logging using the specified file path
        /// </summary>
        /// <param name="loggingBuilder">Logging builder</param>
        /// <param name="filePath">config file path</param>
        /// <returns>Logging builder</returns>
        public static ILoggingBuilder AddGenAiLogging(this ILoggingBuilder loggingBuilder, string filePath)
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddNLog(filePath);

            return loggingBuilder;
        }
    }
}
