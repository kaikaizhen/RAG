using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Context;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Context;
using RAG.SpaceRuntime;
using RAG.Spaces;

namespace RAG.Controllers
{
    /// <summary>
    /// 知識檢索（只檢索、不生成答案）：供使用者自己的 AI（Claude / Codex）作為上下文
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [RequireSpaceContext]
    [Tags("知識檢索模組")]
    public class KnowledgeController : ControllerBase
    {
        private const string GroundedNotice = "evidence 為知識庫中的依據；status 為 Stale / Inferred 者可信度較低。similar 僅為類似答案，不可當作依據。";
        private const string InsufficientNotice = "知識庫中沒有足夠依據。similar 僅為類似答案，不可當作依據；請改由你自己的判斷或向使用者確認。";
        private readonly ISpaceContextAccessor _spaceContextAccessor;
        private readonly IKnowledgeSearchService _knowledgeSearch;

        public KnowledgeController(
            ISpaceContextAccessor spaceContextAccessor,
            IKnowledgeSearchService knowledgeSearch
        )
        {
            _spaceContextAccessor = spaceContextAccessor;
            _knowledgeSearch = knowledgeSearch;
        }

        /// <summary>
        /// 在目前 Space 的知識庫檢索（回傳證據與類似答案，不產生答案）
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(KnowledgeSearchVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Search([FromBody] KnowledgeSearchParam request)
        {
            var result = await _knowledgeSearch.SearchAsync(_spaceContextAccessor.Current, request.Query, request.Type, HttpContext.RequestAborted);

            return Ok(new KnowledgeSearchVM
            {
                SpaceID = result.SpaceId,
                SpaceKey = result.SpaceKey,
                Status = result.Status,
                Evidence = result.Evidence.Select(ToVM).ToList(),
                Similar = result.Similar.Select(ToVM).ToList(),
                Notice = result.Status == KnowledgeSearchStatus.Insufficient ? InsufficientNotice : GroundedNotice
            });
        }

        private static KnowledgeItemVM ToVM(KnowledgeItem item) => new()
        {
            QuestionID = item.QuestionId,
            Question = item.Question,
            Answer = item.Answer,
            Score = Math.Round(item.Score, 4),
            Status = item.Status
        };
    }
}
