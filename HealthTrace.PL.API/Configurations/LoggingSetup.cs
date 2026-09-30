using Serilog;
using Serilog.Formatting.Compact;

namespace HealthTrace.PL.API.Configurations
{
    /// <summary>
    /// Builds the Serilog configuration. The console always receives events,
    /// the file only outside Development, where it is useful for post-mortem diagnosis
    /// (locally it would only get in the way). Minimum levels and per-category overrides
    /// come from the "Serilog" section of appsettings.json. It must be registered on the
    /// builder services before Build().
    /// </summary>
    public static class LoggingSetup
    {
        private const long FileSizeLimitBytes = 10 * 1024 * 1024;

        public static void AddSerilogLogging(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddSerilog((_, loggerConfiguration) =>
                ConfigureLogger(loggerConfiguration, configuration, environment));
        }

        private static void ConfigureLogger(
            LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var fileOptions = configuration
                .GetSection(FileLoggingOptions.SectionName)
                .Get<FileLoggingOptions>() ?? new FileLoggingOptions();

            loggerConfiguration
                .MinimumLevel.Information()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            if (environment.IsDevelopment())
                return;

            loggerConfiguration.WriteTo.File(
                path: fileOptions.Path,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimitBytes,
                retainedFileCountLimit: fileOptions.RetainedFileCountLimit,
                shared: true,
                formatter: new RenderedCompactJsonFormatter());
        }
    }
}
