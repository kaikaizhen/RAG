using RAG.Commons.Enums;

namespace RAG.Models.Params.Question
{
    public class GetQuestionParam
    { 
        /// <summary>
        /// 使用者編號
        /// </summary>
        public required int UserID {  get; set; }

        /// <summary>
        /// 種類編號
        /// </summary>
        public int? Id { get; set; } = null;

        /// <summary>
        /// 查詢種類
        /// </summary>
        public QuestionEnum Type { get; set; }
    }
}
