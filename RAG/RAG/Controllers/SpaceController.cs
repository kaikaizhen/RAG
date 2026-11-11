using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Authentication;
using RAG.Authentication.ApiTokens;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Space;
using RAG.SpaceRuntime;
using RAG.Spaces;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Tags("Space 功能模組")]
    public class SpaceController : ControllerBase
    {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly ISpaceMembershipStore _membershipStore;
        private readonly ISpaceContextAccessor _spaceContextAccessor;

        public SpaceController(
            ICurrentUserAccessor currentUser,
            ISpaceMembershipStore membershipStore,
            ISpaceContextAccessor spaceContextAccessor
        )
        {
            _currentUser = currentUser;
            _membershipStore = membershipStore;
            _spaceContextAccessor = spaceContextAccessor;
        }

        /// <summary>
        /// 查詢目前使用者可使用的 Space 清單
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = ApiPolicies.SpaceRead)]
        [ProducesResponseType(typeof(List<SpaceVM>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get()
        {
            var memberships = await _membershipStore.GetActiveMembershipsAsync(_currentUser.UserId, HttpContext.RequestAborted);

            // 限定 Space 的應用程式權杖只看得到被允許的那幾個
            var tokenSpaces = User.HasClaim(ApiTokenClaims.TokenUse, ApiTokenClaims.PersonalAccessToken)
                ? User.FindAll(ApiTokenClaims.AllowedSpace).Select(c => int.Parse(c.Value)).ToHashSet()
                : new HashSet<int>();

            return Ok(memberships
                .Where(m => tokenSpaces.Count == 0 || tokenSpaces.Contains(m.SpaceId))
                .Where(m => SpaceRoles.TryGetPermissions(m.Role, out _))
                .Select(m => new SpaceVM { SpaceID = m.SpaceId, Key = m.SpaceKey, Name = m.SpaceName, Role = m.Role, IsDefault = m.IsDefault })
                .ToList());
        }

        /// <summary>
        /// 解析目前 request 的 Space（以 X-Space-Id header 作為選擇提示）
        /// </summary>
        /// <returns></returns>
        [HttpGet("current")]
        [Authorize(Policy = ApiPolicies.SpaceRead)]
        [RequireSpaceContext]
        [ProducesResponseType(typeof(CurrentSpaceVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        public IActionResult GetCurrent()
        {
            var context = _spaceContextAccessor.Current;

            return Ok(new CurrentSpaceVM
            {
                SpaceID = context.Space.SpaceId,
                Key = context.Space.SpaceKey,
                Name = context.Space.SpaceName,
                Role = context.Space.Role,
                Permissions = context.Space.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToList(),
                RepositoryID = context.Resource.RepositoryId,
                ProjectID = context.Resource.ProjectId
            });
        }
    }
}
