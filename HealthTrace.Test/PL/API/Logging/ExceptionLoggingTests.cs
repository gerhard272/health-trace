using System.Net;
using System.Text;
using System.Text.Json;
using HealthTrace.BLL.Exceptions;
using HealthTrace.BLL.Models;
using HealthTrace.BLL.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Serilog.Extensions.Logging;

namespace HealthTrace.Test.PL.API.Logging
{
    [Collection(SerilogCollection.Name)]
    public class ExceptionLoggingTests
    {
        private const string LoginPath = "/api/auth/login";
        

        [Fact]
        public void Logger_factory_is_SerilogLoggerFactory()
        {
            using var host = new LoggingTestHost(Environments.Production);
            using var client = host.CreateClient();

            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

            Assert.IsType<SerilogLoggerFactory>(loggerFactory);
        }

        [Fact]
        public async Task NotFound_logs_structured_properties()
        {
            using var host = HostThrowing(new NotFoundException("Symptom", 99));
            using var client = host.CreateClient();

            var response = await client.PostAsync(LoginPath, LoginBody());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var eventJson = JsonDocument.Parse(LineFor(host, "NotFoundException"));
            var written = eventJson.RootElement;

            Assert.Equal("Warning", written.GetProperty("@l").GetString());
            Assert.Equal("NotFoundException", written.GetProperty("ErrorType").GetString());
            Assert.Equal("Symptom", written.GetProperty("ResourceName").GetString());
            Assert.Equal(99, written.GetProperty("ResourceKey").GetInt32());
            Assert.False(written.TryGetProperty("@x", out _));
        }

        [Fact]
        public async Task Validation_logs_fields_and_count()
        {
            var errors = new Dictionary<string, string[]>
            {
                ["username"] = ["Username already in use"],
                ["cf"] = ["Fiscal code already registered", "Format invalid"]
            };

            using var host = HostThrowing(new ValidationException(errors));
            using var client = host.CreateClient();

            var response = await client.PostAsync(LoginPath, LoginBody());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var eventJson = JsonDocument.Parse(LineFor(host, "ValidationException"));
            var written = eventJson.RootElement;

            Assert.Equal("Warning", written.GetProperty("@l").GetString());
            Assert.Equal(3, written.GetProperty("ErrorCount").GetInt32());

            var fields = written.GetProperty("ErrorFields")
                .EnumerateArray()
                .Select(field => field.GetString())
                .ToArray();

            Assert.Equal(2, fields.Length);
            Assert.Contains("username", fields);
            Assert.Contains("cf", fields);
        }

        [Fact]
        public async Task Unexpected_exception_returns_500_with_stack_trace()
        {
            using var host = HostThrowing(new FormatException("Unexpected failure"));
            using var client = host.CreateClient();

            var response = await client.PostAsync(LoginPath, LoginBody());

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            var line = host.ReadLoggedLines()
                .Single(entry => entry.Contains("\"@l\":\"Error\"", StringComparison.Ordinal));

            using var eventJson = JsonDocument.Parse(line);
            var stackTrace = eventJson.RootElement.GetProperty("@x").GetString();

            Assert.NotNull(stackTrace);
            Assert.Contains("FormatException", stackTrace, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Trace_id_matches_between_response_and_log()
        {
            using var host = HostThrowing(new NotFoundException("Symptom", 99));
            using var client = host.CreateClient();

            var response = await client.PostAsync(LoginPath, LoginBody());

            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var traceId = problem.RootElement.GetProperty("traceId").GetString();

            Assert.False(string.IsNullOrWhiteSpace(traceId));

            var logged = JsonDocument.Parse(LineFor(host, "NotFoundException"))
                .RootElement.GetProperty("@tr").GetString();

            Assert.NotNull(logged);
            Assert.Contains(logged!, traceId, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Log_file_not_created_in_development()
        {
            using var host = HostThrowing(new NotFoundException("Symptom", 99), Environments.Development);
            using var client = host.CreateClient();

            var response = await client.PostAsync(LoginPath, LoginBody());

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(host.LogFileExists);
        }

        [Fact]
        public void Empty_log_path_stops_startup()
        {
            using var host = new LoggingTestHost(
                Environments.Production,
                configureServices: null,
                overrides: new Dictionary<string, string?> { ["FileLogging:Path"] = string.Empty });

            var thrown = Record.Exception(() => host.CreateClient());

            Assert.NotNull(thrown);
            Assert.Contains(typeof(OptionsValidationException), ExceptionChain(thrown));
        }

        private static LoggingTestHost HostThrowing(Exception exception, string? environment = null)
        {
            var userService = new Mock<IUserService>();
            userService
                .Setup(service => service.LoginAsync(It.IsAny<LoginModel>(), It.IsAny<CancellationToken>()))
                .Throws(exception);

            return new LoggingTestHost(environment ?? Environments.Production, services =>
            {
                services.RemoveAll<IUserService>();
                services.AddScoped(_ => userService.Object);
            });
        }

        private static StringContent LoginBody() => new(
            "{\"username\":\"user\",\"password\":\"password\"}",
            Encoding.UTF8,
            "application/json");

        private static string LineFor(LoggingTestHost host, string errorType)
        {
            var line = host.ReadLoggedLines()
                .SingleOrDefault(entry => entry.Contains($"\"ErrorType\":\"{errorType}\"", StringComparison.Ordinal));

            Assert.NotNull(line);
            return line;
        }

        private static IEnumerable<Type> ExceptionChain(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
                yield return current.GetType();
        }
    }
}
