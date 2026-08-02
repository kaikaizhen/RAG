using Newtonsoft.Json;

namespace RAG.Models.Dtos.RagServer
{
    public class ChatReponseDto
    {
        /// <summary>
        /// 答案描述
        /// </summary>
        [JsonProperty("answer")]
        public string Answer {  get; set; } =string.Empty;

        /// <summary>
        /// 置信度（confidence 分數）
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 相關問題（模型檢索出的類似問題）
        /// </summary>
        public List<string> Related { get; set; } = new List<string>();

        /// <summary>
        /// 使用的回覆方法（qa_model / direct_match / context_summary）
        /// </summary>
        public string Method { get; set; }=string.Empty;
    }
}
