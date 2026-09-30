using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace HealthTrace.Test.PL.API.Logging
{
    public sealed class LoggingTestHost : WebApplicationFactory<Program>
    {
        private const string LogPattern = "healthtrace-*.log";

        private readonly string _environment;
        private readonly Action<IServiceCollection>? _configureServices;
        private readonly IReadOnlyDictionary<string, string?>? _overrides;
        private readonly string _logDirectory;
        private readonly string _logPath;

        public LoggingTestHost(
            string environment,
            Action<IServiceCollection>? configureServices = null,
            IReadOnlyDictionary<string, string?>? overrides = null)
        {
            _environment = environment;
            _configureServices = configureServices;
            _overrides = overrides;
            _logDirectory = Path.Combine(Path.GetTempPath(), "HealthTraceLoggingTests", Guid.NewGuid().ToString("N"));
            _logPath = Path.Combine(_logDirectory, "healthtrace-.log");
        }

        public bool LogFileExists =>
            Directory.Exists(_logDirectory) && Directory.GetFiles(_logDirectory, LogPattern).Length > 0;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);

            builder.ConfigureAppConfiguration(configuration =>
            {
                var values = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["FileLogging:Path"] = _logPath,
                    ["FileLogging:RetainedFileCountLimit"] = "1",
                    ["BlobStorage:ConnectionString"] = "not-a-connection-string",
                    ["ConnectionStrings:HealthTraceDb"] =
                        "Server=localhost;Database=HealthTraceTest;User Id=test;Password=test;TrustServerCertificate=true"
                };

                if (_overrides is not null)
                {
                    foreach (var entry in _overrides)
                        values[entry.Key] = entry.Value;
                }

                configuration.AddInMemoryCollection(values);
            });

            if (_configureServices is not null)
                builder.ConfigureServices(_configureServices);
        }

        public IReadOnlyList<string> ReadLoggedLines()
        {
            Log.CloseAndFlush();

            if (!Directory.Exists(_logDirectory))
                return Array.Empty<string>();

            return Directory.GetFiles(_logDirectory, LogPattern)
                .SelectMany(File.ReadAllLines)
                .ToList();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing || !Directory.Exists(_logDirectory))
                return;

            try
            {
                Directory.Delete(_logDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
