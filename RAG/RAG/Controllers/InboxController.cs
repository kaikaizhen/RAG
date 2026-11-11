using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RAG.Authentication.ApiTokens;
using RAG.Inbox;
using RAG.Models.Entities;
using RAG.Models.Params.Inbox;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Inbox;
using RAG.Spaces;

namespace RAG.Controllers
{
    /// <summary>
    /// 暫存區：外部服務與使用者自己的 AI 只能把問答放到這裡；本人審查接受後才會寫入知識庫與向量資料庫
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [RequireSpaceContext]
    [Tags("暫存區模組")]
    public class InboxController : ControllerBase
    {
        private readonly IInboxService _inbox;

        public InboxController(IInboxService inbox)
        {
            _inbox = inbox;
        }

        /// <summary>
        /// 放一筆問答到暫存區（登入或應用程式權杖皆可；權杖需要 inbox.write）。同一份內容重送會回傳原本那筆
        /// </summary>
        [HttpPost]
        [Authorize(Policy = ApiPolicies.InboxWrite)]
        [EnableRateLimiting(InboxRateLimits.Write)]
        [ProducesResponseType(typeof(DraftVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateDraftParam request)
        {
            var source = User.FindFirst(ApiTokenClaims.TokenName)?.Value ?? "web";
            var draft = await _inbox.CreateAsync(source, request.Question, request.Answer, request.QaItemTypeID, request.SourceRef, HttpContext.RequestAborted);
            return Ok(ToVM(draft));
        }

        /// <summary>
        /// 查詢我在目前 Space 的草稿（預設為待審）
        /// </summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(List<DraftVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] string status = DraftStatus.Pending)
        {
            var drafts = await _inbox.ListAsync(status, HttpContext.RequestAborted);
            return Ok(drafts.Select(ToVM).ToList());
        }

        /// <summary>
        /// 修改待審草稿的內容
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(DraftVM), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateDraftParam request)
        {
            var draft = await _inbox.UpdateAsync(id, request.Question, request.Answer, request.QaItemTypeID, HttpContext.RequestAborted);
            return Ok(ToVM(draft));
        }

        /// <summary>
        /// 接受草稿：此時才寫入知識庫並產生向量（需要該 Space 的 knowledge.write）
        /// </summary>
        [HttpPost("{id}/accept")]
        [Authorize]
        [ProducesResponseType(typeof(DraftVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Accept([FromRoute] int id, [FromBody] AcceptDraftParam request)
        {
            var draft = await _inbox.AcceptAsync(id, request.QaItemTypeID, request.Status, HttpContext.RequestAborted);
            return Ok(ToVM(draft));
        }

        /// <summary>
        /// 拒絕草稿（保留紀錄，不會進知識庫）
        /// </summary>
        [HttpPost("{id}/reject")]
        [Authorize]
        [ProducesResponseType(typeof(DraftVM), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reject([FromRoute] int id)
        {
            var draft = await _inbox.RejectAsync(id, HttpContext.RequestAborted);
            return Ok(ToVM(draft));
        }

        private static DraftVM ToVM(KnowledgeDraft draft) => new()
        {
            ID = draft.ID,
            SpaceID = draft.SpaceID,
            Question = draft.Question,
            Answer = draft.Answer,
            QaItemTypeID = draft.QaItemTypeID,
            Source = draft.Source,
            SourceRef = draft.SourceRef,
            Status = draft.Status,
            AcceptedQuestionID = draft.AcceptedQuestionID,
            CreateAt = draft.CreateAt,
            ReviewedAt = draft.ReviewedAt
        };
    }
}
