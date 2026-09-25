using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RAG.Rag;
using RAG.Models.Entities;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace RAG.Tests.Infrastructure
{
    /// <summary>
    /// 以真實 Program / middleware pipeline 啟動 API；僅以 InMemory DB 與測試專用 JWT secret 檔取代外部依賴
    /// </summary>
    public class RagApiFactory : WebApplicationFactory<Program>
    {
        public string SecretKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        public string SecretFilePath { get; }

        /// <summary>
        /// 取代真實 RAG Server 的 gateway（記錄呼叫的 RagScope）
        /// </summary>
        public FakeRagGateway RagGateway { get; } = new();

        private readonly string _databaseName = $"rag-tests-{Guid.NewGuid()}";

        public RagApiFactory() : this(secretFilePath: null) { }

        protected RagApiFactory(string? secretFilePath)
        {
            if (secretFilePath is null)
            {
                SecretFilePath = Path.Combine(Path.GetTempPath(), $"rag-tests-{Guid.NewGuid()}.json");
                File.WriteAllText(SecretFilePath, JsonSerializer.Serialize(new Dictionary<string, string> { ["JWT_SECRET_KEY"] = SecretKey }));
            }
            else
            {
                SecretFilePath = secretFilePath;
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:SecretFilePath", SecretFilePath);
            builder.UseSetting("RagOption:InternalApiKey", "test-internal-key");

            builder.ConfigureServices(services =>
            {
                var dbDescriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<WenDaHubContext>)
                             || d.ServiceType == typeof(DbContextOptions)
                             || d.ServiceType == typeof(IDbContextOptionsConfiguration<WenDaHubContext>)
                             || d.ServiceType == typeof(WenDaHubContext))
                    .ToList();
                foreach (var descriptor in dbDescriptors) services.Remove(descriptor);

                services.RemoveAll<IRagGateway>();
                services.AddSingleton<IRagGateway>(RagGateway);

                services.AddDbContext<WenDaHubContext>(options => options
                    .UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            });
        }

        public HttpClient CreateClientWithToken(string? token)
        {
            var client = CreateClient();
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            return client;
        }

        public async Task WithDbAsync(Func<WenDaHubContext, Task> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WenDaHubContext>();
            await action(db);
        }

        public async Task<T> WithDbAsync<T>(Func<WenDaHubContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WenDaHubContext>();
            return await action(db);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(SecretFilePath) && Path.GetFileName(SecretFilePath).StartsWith("rag-tests-"))
            {
                File.Delete(SecretFilePath);
            }
        }
    }
}
