using Serilog;
using Serilog.Formatting.Compact;

namespace HealthTrace.PL.API.Configurations
{
    /// <summary>
    /// Costruzione della configurazione Serilog. La console riceve sempre gli eventi,
    /// il file solo fuori da Development, dove serve alla diagnosi a posteriori e in
    /// locale può solo ingombrare. I livelli minimi e gli override per categoria
    /// arrivano dalla sezione "Serilog" di appsettings.json. Va registrata sui servizi
    /// del builder prima del build.
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
