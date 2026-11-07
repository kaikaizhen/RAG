using Newtonsoft.Json;

namespace RAG.Models.Dtos.RagServer
{
    public class RagSearchResponse
    {
        [JsonProperty("hits")]
        public List<RagSearchHit> Hits { get; set; } = new();
    }

    /// <summary>
    /// RAG Server /search 命中結果（內容僅供參考；權威內容與狀態以 MySQL 回查為準）
    /// </summary>
    public class RagSearchHit
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("reference_id")]
        public string? ReferenceId { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("question")]
        public string Question { get; set; } = string.Empty;

        [JsonProperty("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonProperty("score")]
        public double Score { get; set; }
    }
}
