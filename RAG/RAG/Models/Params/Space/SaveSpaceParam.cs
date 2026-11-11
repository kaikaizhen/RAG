using System.ComponentModel.DataAnnotations;

namespace RAG.Models.Params.Space
{
    public class SaveSpaceParam
    {
        /// <summary>
        /// 空間名稱（1–100 字）
        /// </summary>
        [Required, StringLength(100, MinimumLength = 1)]
        public required string Name { get; set; }
    }
}
