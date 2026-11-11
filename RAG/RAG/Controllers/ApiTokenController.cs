using Asp.Versioning;
using Library.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Authentication;
using RAG.Authentication.ApiTokens;
using RAG.Models.Entities;
using RAG.Models.Params.ApiToken;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.ApiToken;
using RAG.SpaceRuntime;
using System.Net;

namespace RAG.Controllers
{
    /// <summary>
    /// 應用程式權杖的管理。只接受登入 token：權杖不能自己建立或撤銷權杖
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Tags("應用程式權杖模組")]
    public class ApiTokenController : ControllerBase
    {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IApiTokenService _tokens;
        private readonly ISpaceMembershipStore _membershipStore;

        public ApiTokenController(ICurrentUserAccessor currentUser, IApiTokenService tokens, ISpaceMembershipStore membershipStore)
        {
            _currentUser = currentUser;
            _tokens = tokens;
            _membershipStore = membershipStore;
        }

        /// <summary>
        /// 查詢我的權杖（不含明文）
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<ApiTokenVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var tokens = await _tokens.ListAsync(_currentUser.UserId, HttpContext.RequestAborted);
            return Ok(tokens.Select(ToVM).ToList());
        }

        /// <summary>
        /// 建立權杖。明文只會在這次回應出現一次
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateApiTokenVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateApiTokenParam request)
        {
            var invalid = request.Scopes.Where(s => !ApiTokenScopes.IsValid(s)).ToList();
            if (invalid.Count > 0) throw new HttpException(HttpStatusCode.BadRequest, $"不支援的權限範圍：{string.Join(", ", invalid)}");

            var userId = _currentUser.UserId;
            if (request.SpaceIDs is { Count: > 0 })
            {
                // 只能限定在自己有 membership 的 Space；伺服器不會因為權杖就多給任何權限
                var mine = (await _membershipStore.GetActiveMembershipsAsync(userId, HttpContext.RequestAborted)).Select(m => m.SpaceId).ToHashSet();
                if (request.SpaceIDs.Any(id => !mine.Contains(id))) throw new HttpException(HttpStatusCode.BadRequest, "只能選擇你有加入的 Space");
            }

            var (token, plaintext) = await _tokens.CreateAsync(userId, request.Name, request.Scopes, request.SpaceIDs, request.ExpiresInDays, HttpContext.RequestAborted);
            var vm = new CreateApiTokenVM { Token = plaintext };
            Fill(vm, token);
            return Ok(vm);
        }

        /// <summary>
        /// 撤銷權杖（立即失效）
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Revoke([FromRoute] int id)
        {
            await _tokens.RevokeAsync(_currentUser.UserId, id, HttpContext.RequestAborted);
            return Ok(new { State = "Success", Message = $"Revoke ApiToken{id} Success" });
        }

        private static ApiTokenVM ToVM(ApiToken token)
        {
            var vm = new ApiTokenVM();
            Fill(vm, token);
            return vm;
        }

        private static void Fill(ApiTokenVM vm, ApiToken token)
        {
            vm.ID = token.ID;
            vm.Name = token.Name;
            vm.TokenPrefix = token.TokenPrefix;
            vm.Scopes = ApiTokenService.SplitScopes(token.Scopes).ToList();
            vm.SpaceIDs = ApiTokenService.SplitIds(token.SpaceIDs).ToList();
            vm.CreateAt = token.CreateAt;
            vm.ExpiresAt = token.ExpiresAt;
            vm.LastUsedAt = token.LastUsedAt;
        }
    }
}
