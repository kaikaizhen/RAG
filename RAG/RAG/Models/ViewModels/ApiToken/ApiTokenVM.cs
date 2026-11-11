namespace RAG.Models.ViewModels.ApiToken
{
    public class ApiTokenVM
    {
        public int ID { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 權杖前幾碼，用來辨識是哪一把
        /// </summary>
        public string TokenPrefix { get; set; } = string.Empty;

        public List<string> Scopes { get; set; } = new();

        /// <summary>
        /// 限定的 Space；空陣列表示你所有的 Space
        /// </summary>
        public List<int> SpaceIDs { get; set; } = new();

        public DateTime CreateAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }

    public class CreateApiTokenVM : ApiTokenVM
    {
        /// <summary>
        /// 權杖明文。只會在建立時回傳這一次，之後無法再取得；請立刻複製保存
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}
