using Newtonsoft.Json;

namespace RAG.Models.Dtos.RagServer
{
    public class ToVectorDto
    {
        /// <summary>
        /// 問題列表（對應 Python 的 "questions" 屬性）
        /// </summary>
        [JsonProperty("space_id")]
        public int SpaceId { get; set; }

        [JsonProperty("questions")]
        public List<QuestionRequest> Questions { get; set; } = new List<QuestionRequest>();

        public class QuestionRequest
        {
            /// <summary>
            /// 問題編號
            /// </summary>
            [JsonProperty("id")]
            public int Id { get; set; }

            /// <summary>
            /// 向量參考編號
            /// </summary>
            [JsonProperty("referenceId", NullValueHandling = NullValueHandling.Ignore)]
            public string? ReferenceID { get; set; }

            /// <summary>
            /// 問題種類編號
            /// </summary>
            [JsonProperty("qaTypeId")]
            public int QaTypeId { get; set; }

            /// <summary>
            /// 問題種類細項編號
            /// </summary>
            [JsonProperty("qaItemTypeId")]
            public int QaItemTypeId { get; set; }

            /// <summary>
            /// 問題內容
            /// </summary>
            [JsonProperty("question")]
            public string Question { get; set; } = string.Empty;

            /// <summary>
            /// 答案內容
            /// </summary>
            [JsonProperty("answer")]
            public string Answer { get; set; } = string.Empty;
        }
    }
}
