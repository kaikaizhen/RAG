using Library.Http.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RAG.Authentication;
using RAG.Tests.Infrastructure;
using System.Text.Json;
using TD_WebApiHelper.Core.Helpers;

namespace RAG.Tests.Authentication
{
    public class JwtStartupTests
    {
        private static IConfiguration ConfigWithSecretFile(string path) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SecretFilePath"] = path }).Build();

        private static string WriteTempConfig(object content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"rag-tests-{Guid.NewGuid()}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(content));
            return path;
        }

        [Fact]
        public void Load_reads_existing_secret_with_compatible_issuer_and_audience()
        {
            var path = WriteTempConfig(new Dictionary<string, string> { ["JWT_SECRET_KEY"] = new string('k', 44) });
            try
            {
                var options = JwtOptions.Load(ConfigWithSecretFile(path));

                Assert.Equal("Kaikai", options.Issuer);
                Assert.Equal("Audience", options.Audience);
                Assert.Equal(44, options.SigningKey.Length);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_fails_fast_and_never_creates_secret_when_file_missing()
        {
            var path = Path.Combine(Path.GetTempPath(), $"rag-tests-missing-{Guid.NewGuid()}.json");

            Assert.Throws<InvalidOperationException>(() => JwtOptions.Load(ConfigWithSecretFile(path)));
            Assert.False(File.Exists(path));
        }

        public static TheoryData<object> InvalidConfigs => new()
        {
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["JWT_SECRET_KEY"] = "" },
            new Dictionary<string, string> { ["JWT_SECRET_KEY"] = "<請以 base64 隨機字串取代>" },
            new Dictionary<string, string> { ["JWT_SECRET_KEY"] = "too-short" },
        };

        [Theory]
        [MemberData(nameof(InvalidConfigs))]
        public void Load_fails_fast_and_never_overwrites_invalid_secret(object content)
        {
            var path = WriteTempConfig(content);
            var before = File.ReadAllText(path);
            try
            {
                Assert.Throws<InvalidOperationException>(() => JwtOptions.Load(ConfigWithSecretFile(path)));
                Assert.Equal(before, File.ReadAllText(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Api_does_not_start_without_secret_file()
        {
            var missing = Path.Combine(Path.GetTempPath(), $"rag-tests-missing-{Guid.NewGuid()}.json");
            using var factory = new MissingSecretFactory(missing);

            Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.False(File.Exists(missing));
        }

        [Fact]
        public void Library_jwt_helper_is_replaced_and_executor_is_not_an_authentication_authority()
        {
            using var factory = new RagApiFactory();
            using var scope = factory.Services.CreateScope();

            var helper = scope.ServiceProvider.GetRequiredService<IJwtTokenHelper>();
            Assert.IsType<DisabledLibraryJwtTokenHelper>(helper);
            Assert.Throws<NotSupportedException>(() => helper.ValidateToken("any"));

            // ApiExecutorHandler 仍可建構（僅作例外 → HTTP status 轉換），但不會觸發 Library secret 產生
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IApiExecutorHandler>());
        }

        private sealed class MissingSecretFactory : RagApiFactory
        {
            public MissingSecretFactory(string secretFilePath) : base(secretFilePath) { }
        }
    }
}
