namespace RAG.Models.ViewModels.Inbox
{
    public class DraftVM
    {
        public int ID { get; set; }

        public int SpaceID { get; set; }

        public string Question { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        public int? QaItemTypeID { get; set; }

        /// <summary>
        /// 來源：權杖名稱，或 web
        /// </summary>
        public string Source { get; set; } = string.Empty;

        public string? SourceRef { get; set; }

        /// <summary>
        /// Pending / Accepted / Rejected
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 接受後建立的正式問題編號
        /// </summary>
        public int? AcceptedQuestionID { get; set; }

        public DateTime CreateAt { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }
}
