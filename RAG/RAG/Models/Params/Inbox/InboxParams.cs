using System.ComponentModel.DataAnnotations;

namespace RAG.Models.Params.Inbox
{
    public class CreateDraftParam
    {
        /// <summary>
        /// 問題
        /// </summary>
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Question { get; set; }

        /// <summary>
        /// 解答（Markdown）
        /// </summary>
        [Required, StringLength(20000, MinimumLength = 1)]
        public required string Answer { get; set; }

        /// <summary>
        /// 建議的問題種類細項；之後審查時還能改
        /// </summary>
        public int? QaItemTypeID { get; set; }

        /// <summary>
        /// 來源端自己的識別（例如筆記編號或網址），方便對方對應回去
        /// </summary>
        [StringLength(500)]
        public string? SourceRef { get; set; }
    }

    public class UpdateDraftParam
    {
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Question { get; set; }

        [Required, StringLength(20000, MinimumLength = 1)]
        public required string Answer { get; set; }

        public int? QaItemTypeID { get; set; }
    }

    public class AcceptDraftParam
    {
        /// <summary>
        /// 寫入哪個問題種類細項；草稿沒有建議時必填
        /// </summary>
        public int? QaItemTypeID { get; set; }

        /// <summary>
        /// 知識狀態；不帶時為 Inferred（來自外部、經你審查接受，但不是你親手寫的）
        /// </summary>
        public string? Status { get; set; }
    }
}
