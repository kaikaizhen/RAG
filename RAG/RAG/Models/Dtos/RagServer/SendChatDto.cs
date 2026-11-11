using Newtonsoft.Json;

namespace RAG.Models.Dtos.RagServer
{
    public class SendChatDto
    {
        /// <summary>
        /// Space 範圍（僅由 RagGateway 依 RagScope 設定）
        /// </summary>
        [JsonProperty("space_id")]
        public int SpaceId { get; set; }

        /// <summary>
        /// 公開知識的 Space（僅由 RagGateway 依伺服器設定帶入）
        /// </summary>
        [JsonProperty("shared_space_id", NullValueHandling = NullValueHandling.Ignore)]
        public int? SharedSpaceId { get; set; }

        /// <summary>
        /// 檢索涵蓋的 Space（僅由 RagGateway 依 RagScope 帶入，且只在涵蓋多個 Space 時才送）
        /// </summary>
        [JsonProperty("space_ids", NullValueHandling = NullValueHandling.Ignore)]
        public List<int>? SpaceIds { get; set; }

        /// <summary>
        /// 問題描述
        /// </summary>
        [JsonProperty("question")]
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 搜尋範圍
        /// </summary>
        [JsonProperty("type")]
        public string? Type { get; set; } = null;
    }
}
