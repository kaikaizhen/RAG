using Library.Http;
using Newtonsoft.Json;
using RAG.Commons.Options;
using RAG.Models.Dtos.RagServer;
using RAG.SpaceRuntime;
using System.Net;
using System.Text;

namespace RAG.Rag
{
    public class RagGateway : IRagGateway
    {
        public const string InternalApiKeyHeader = "X-Internal-Api-Key";

        private readonly HttpClient _httpClient;
        private readonly RagOption _ragOption;

        public RagGateway(HttpClient httpClient, RagOption ragOption)
        {
            _httpClient = httpClient;
            _ragOption = ragOption;
        }

        public async Task<string> UpsertQuestionAsync(RagScope scope, ToVectorDto.QuestionRequest question, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var content = new ToVectorDto
            {
                SpaceId = scope.SpaceId,
                Questions = new List<ToVectorDto.QuestionRequest> { question }
            };

            using var response = await SendAsync(HttpMethod.Post, $"{_ragOption.BaseUrl}{_ragOption.ToVectorUrl}", content, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpException(HttpStatusCode.InternalServerError, "ToVector API Error");

            var vectorIds = JsonConvert.DeserializeObject<VectorReponseDto>(await response.Content.ReadAsStringAsync(cancellationToken));
            return vectorIds?.VectorIds.FirstOrDefault() ?? throw new HttpException(HttpStatusCode.InternalServerError, "ToVector API Error");
        }

        public async Task<ChatReponseDto> AskAsync(RagScope scope, string question, string? type, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var content = new SendChatDto { SpaceId = scope.SpaceId, Question = question, Type = type };

            using var response = await SendAsync(HttpMethod.Post, $"{_ragOption.BaseUrl}{_ragOption.AskUrl}", content, cancellationToken);
            // RAG Server 以 400 表示「沒有足夠可靠的相關內容」（低於門檻或查無資料），屬正常的查無結果，不是伺服器錯誤
            if (response.StatusCode == HttpStatusCode.BadRequest) throw new HttpException(HttpStatusCode.NotFound, "找不到足夠可靠的相關資料");
            if (!response.IsSuccessStatusCode) throw new HttpException(HttpStatusCode.InternalServerError, "Ask API Error");

            return JsonConvert.DeserializeObject<ChatReponseDto>(await response.Content.ReadAsStringAsync(cancellationToken))
                ?? throw new HttpException(HttpStatusCode.InternalServerError, "Ask API Error");
        }

        public async Task<IReadOnlyList<RagSearchHit>> SearchAsync(RagScope scope, string query, int limit, string? type, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var content = new { space_id = scope.SpaceId, query, limit, type };

            using var response = await SendAsync(HttpMethod.Post, $"{_ragOption.BaseUrl}{_ragOption.SearchUrl}", content, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpException(HttpStatusCode.InternalServerError, "Search API Error");

            var result = JsonConvert.DeserializeObject<RagSearchResponse>(await response.Content.ReadAsStringAsync(cancellationToken));
            return result?.Hits ?? throw new HttpException(HttpStatusCode.InternalServerError, "Search API Error");
        }

        public async Task DeleteQuestionAsync(RagScope scope, string vectorId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scope);

            var url = $"{_ragOption.BaseUrl}{_ragOption.DeleteUrl}{Uri.EscapeDataString(vectorId)}?space_id={scope.SpaceId}";

            using var response = await SendAsync(HttpMethod.Delete, url, null, cancellationToken);

            // 404 = scope 內已不存在，視為已刪除，讓中斷後的重試可以完成
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                throw new HttpException(HttpStatusCode.InternalServerError, "DeleteVector API Error");
            }
        }

        private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add(InternalApiKeyHeader, _ragOption.InternalApiKey);

            if (body is not null)
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }

            return _httpClient.SendAsync(request, cancellationToken);
        }
    }
}
