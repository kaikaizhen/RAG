using System.ComponentModel.DataAnnotations;

namespace RAG.Models.Params.ApiToken
{
    public class CreateApiTokenParam
    {
        /// <summary>
        /// 權杖名稱（讓你自己辨識用途，例如「筆記網站」）
        /// </summary>
        [Required, StringLength(50, MinimumLength = 1)]
        public required string Name { get; set; }

        /// <summary>
        /// 權限範圍：knowledge.search / inbox.write / space.read
        /// </summary>
        [Required, MinLength(1)]
        public required List<string> Scopes { get; set; }

        /// <summary>
        /// 限定可用的 Space；不帶或空陣列表示你所有的 Space
        /// </summary>
        public List<int>? SpaceIDs { get; set; }

        /// <summary>
        /// 幾天後過期（1–365）；不帶表示不過期
        /// </summary>
        [Range(1, 365)]
        public int? ExpiresInDays { get; set; }
    }
}
