namespace RAG.Webhooks
{
    public static class WebhookEvents
    {
        public const string DraftCreated = "draft.created";
        public const string DraftAccepted = "draft.accepted";
        public const string DraftRejected = "draft.rejected";

        /// <summary>測試用；不用訂閱，由「送出測試」觸發</summary>
        public const string Ping = "ping";

        public static readonly IReadOnlyList<string> Subscribable = new[] { DraftCreated, DraftAccepted, DraftRejected };

        public static bool IsSubscribable(string? name) => name is not null && Subscribable.Contains(name);
    }
}
