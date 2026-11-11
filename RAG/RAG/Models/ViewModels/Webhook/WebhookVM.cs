namespace RAG.Models.ViewModels.Webhook
{
    public class WebhookVM
    {
        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public List<string> Events { get; set; } = new();

        public bool IsActive { get; set; }

        /// <summary>
        /// 連續送不出去的通知數；達 5 筆會自動停用
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 停用原因（啟用中為空）
        /// </summary>
        public string? DisabledReason { get; set; }

        public DateTime CreateAt { get; set; }
    }

    public class CreateWebhookVM : WebhookVM
    {
        /// <summary>
        /// 簽章密鑰（whsec_…）。只會在建立時回傳這一次；對方用它驗證 X-RAG-Signature
        /// </summary>
        public string Secret { get; set; } = string.Empty;
    }

    public class WebhookDeliveryVM
    {
        public int ID { get; set; }

        public int WebhookID { get; set; }

        /// <summary>
        /// 事件編號（payload.id），對方可用它去重
        /// </summary>
        public string EventID { get; set; } = string.Empty;

        public string Event { get; set; } = string.Empty;

        /// <summary>
        /// Pending（等待送出或重試）/ Succeeded / Dead（重試用完，可手動重送）
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public DateTime? NextAttemptAt { get; set; }

        public int? LastStatusCode { get; set; }

        public string? LastError { get; set; }

        public DateTime CreateAt { get; set; }

        public DateTime? DeliveredAt { get; set; }
    }
}
