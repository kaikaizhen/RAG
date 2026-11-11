using System.ComponentModel.DataAnnotations;

namespace RAG.Models.Params.Webhook
{
    public class SaveWebhookParam
    {
        /// <summary>
        /// 名稱（讓你自己辨識）
        /// </summary>
        [Required, StringLength(50, MinimumLength = 1)]
        public required string Name { get; set; }

        /// <summary>
        /// 接收通知的網址。一律要 https://，且必須是公開網路上的網址
        /// </summary>
        [Required, StringLength(500, MinimumLength = 1)]
        public required string Url { get; set; }

        /// <summary>
        /// 訂閱的事件：draft.created / draft.accepted / draft.rejected
        /// </summary>
        [Required, MinLength(1)]
        public required List<string> Events { get; set; }

        /// <summary>
        /// 是否啟用（只在更新時使用；重新啟用會把連續失敗歸零）
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
