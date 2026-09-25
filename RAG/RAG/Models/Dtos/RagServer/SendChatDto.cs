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
