namespace RAG.Models.Dtos.Question
{
    public class CreateQuestionDto
    {
        /// <summary>
        /// 問題種類細項編號
        /// </summary>
        public int QaItemTypeID { get; set; }

        /// <summary>
        /// 問題
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// 解答
        /// </summary>
        public string Answer { get; set; } = string.Empty;
    }
}
