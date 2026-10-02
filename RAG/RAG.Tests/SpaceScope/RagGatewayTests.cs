using Library.Http;
using Newtonsoft.Json.Linq;
using RAG.Commons.Options;
using RAG.Models.Dtos.RagServer;
using RAG.Rag;
using RAG.SpaceRuntime;
using System.Net;

namespace RAG.Tests.SpaceScope
{
    /// <summary>
    /// 真實 RagGateway：每個請求都帶內部金鑰與 RagScope 的 space_id
    /// </summary>
    public class RagGatewayTests
    {
        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;

            public RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{}")
            {
                _status = status;
                _body = body;
            }

            public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
                return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
            }
        }

        private static readonly RagOption Option = new() { BaseUrl = "http://rag.test", InternalApiKey = "k-123" };

        private static RagScope Scope(int spaceId) => new RagScopeFactory().ForVerifiedRecord(spaceId);

        private static (RagGateway Gateway, RecordingHandler Handler) Create(HttpStatusCode status = HttpStatusCode.OK, string body = "{}")
        {
            var handler = new RecordingHandler(status, body);
            return (new RagGateway(new HttpClient(handler), Option), handler);
        }

        [Fact]
        public async Task Upsert_sends_internal_key_and_scope_space_id()
        {
            var (gateway, handler) = Create(body: "{\"status\":\"success\",\"vectorIds\":[\"v-1\"]}");

            var vectorId = await gateway.UpsertQuestionAsync(Scope(7), new ToVectorDto.QuestionRequest { Id = 1, QaTypeId = 2, QaItemTypeId = 3, Question = "q", Answer = "a" });

            Assert.Equal("v-1", vectorId);
            var (request, body) = Assert.Single(handler.Requests);
            Assert.Equal("k-123", request.Headers.GetValues(RagGateway.InternalApiKeyHeader).Single());
            Assert.Equal(7, JObject.Parse(body!)["space_id"]!.Value<int>());
        }

        [Fact]
        public async Task Ask_maps_no_reliable_content_to_404_not_server_error()
        {
            var (gateway, _) = Create(HttpStatusCode.BadRequest, "{\"detail\":\"no content\"}");

            var ex = await Assert.ThrowsAsync<HttpException>(() => gateway.AskAsync(Scope(9), "question", null));

            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        }

        [Fact]
        public async Task Ask_still_reports_other_failures_as_server_error()
        {
            var (gateway, _) = Create(HttpStatusCode.ServiceUnavailable);

            var ex = await Assert.ThrowsAsync<HttpException>(() => gateway.AskAsync(Scope(9), "question", null));

            Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        [Fact]
        public async Task Ask_sends_scope_space_id()
        {
            var (gateway, handler) = Create(body: "{\"answer\":\"x\",\"related\":[\"r\"]}");

            await gateway.AskAsync(Scope(9), "question", "2");

            var body = JObject.Parse(Assert.Single(handler.Requests).Body!);
            Assert.Equal(9, body["space_id"]!.Value<int>());
            Assert.Equal("2", body["type"]!.Value<string>());
        }

        [Fact]
        public async Task Delete_sends_scope_space_id_and_treats_not_found_as_deleted()
        {
            var (gateway, handler) = Create(HttpStatusCode.NotFound);

            await gateway.DeleteQuestionAsync(Scope(4), "a/b");

            var request = Assert.Single(handler.Requests).Request;
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("http://rag.test/questions/a%2Fb?space_id=4", request.RequestUri!.ToString());
            Assert.Equal("k-123", request.Headers.GetValues(RagGateway.InternalApiKeyHeader).Single());
        }

        [Fact]
        public async Task Delete_server_error_throws()
        {
            var (gateway, _) = Create(HttpStatusCode.InternalServerError);

            await Assert.ThrowsAsync<HttpException>(() => gateway.DeleteQuestionAsync(Scope(4), "v"));
        }

        [Fact]
        public async Task Upsert_without_vector_id_throws()
        {
            var (gateway, _) = Create(body: "{\"status\":\"success\",\"vectorIds\":[]}");

            await Assert.ThrowsAsync<HttpException>(() => gateway.UpsertQuestionAsync(Scope(1), new ToVectorDto.QuestionRequest()));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Rag_scope_requires_positive_space_id(int spaceId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RagScopeFactory().ForVerifiedRecord(spaceId));
        }
    }
}
