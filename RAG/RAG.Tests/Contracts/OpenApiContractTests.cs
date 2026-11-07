using RAG.Tests.Infrastructure;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RAG.Tests.Contracts
{
    /// <summary>
    /// API 的 OpenAPI 文件必須與 SDK 產生型別所用的 snapshot（packages/rag-client-sdk/openapi/rag-api.v1.json）一致。
    /// API 變更後以 UPDATE_OPENAPI_SNAPSHOT=1 執行本測試更新 snapshot，再於 SDK 執行 npm run generate。
    /// </summary>
    public class OpenApiContractTests : IClassFixture<RagApiFactory>
    {
        private readonly RagApiFactory _factory;

        public OpenApiContractTests(RagApiFactory factory) => _factory = factory;

        private static string SnapshotPath()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "RAG.slnx")))
                {
                    return Path.GetFullPath(Path.Combine(dir.FullName, "..", "packages", "rag-client-sdk", "openapi", "rag-api.v1.json"));
                }
            }
            throw new InvalidOperationException("RAG.slnx not found");
        }

        [Fact]
        public async Task Live_openapi_document_matches_sdk_snapshot()
        {
            var live = JsonNode.Parse(await _factory.CreateClient().GetStringAsync("/openapi/v1.json"))!;
            var path = SnapshotPath();

            if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, live.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            }

            Assert.True(File.Exists(path), $"OpenAPI snapshot missing: {path}");
            var snapshot = JsonNode.Parse(File.ReadAllText(path))!;
            Assert.True(JsonNode.DeepEquals(live, snapshot), "API contract changed: run this test with UPDATE_OPENAPI_SNAPSHOT=1, then `npm run generate` in packages/rag-client-sdk");
        }

        [Fact]
        public async Task Openapi_document_is_versioned_and_bearer_protected()
        {
            var doc = JsonNode.Parse(await _factory.CreateClient().GetStringAsync("/openapi/v1.json"))!;
            var paths = doc["paths"]!.AsObject().Select(p => p.Key).ToList();

            Assert.NotEmpty(paths);
            Assert.All(paths, p => Assert.StartsWith("/api/v1/", p));
            Assert.Contains("/api/v1/Auth/Refresh", paths);
            Assert.Equal("bearer", doc["components"]!["securitySchemes"]!["Bearer"]!["scheme"]!.GetValue<string>());
        }
    }
}
