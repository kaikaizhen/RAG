using Newtonsoft.Json;

namespace RAG.Models.Dtos.RagServer
{
    public class VectorReponseDto
    {
        /// <summary>
        /// 狀態
        /// </summary>
        [JsonProperty("status")]
        public string Status {  get; set; } = string.Empty;

        /// <summary>
        /// 向量編號
        /// </summary>
        [JsonProperty("vectorIds")]
        public List<string> VectorIds { get; set; } =new List<string>();
    }
}
