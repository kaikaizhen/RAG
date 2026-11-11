using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RAG.Authentication;
using RAG.Authentication.ApiTokens;
using RAG.Models.Params.Space;
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
        private readonly ISpaceManagementService _spaceManagement;

        public SpaceController(
            ICurrentUserAccessor currentUser,
            ISpaceMembershipStore membershipStore,
            ISpaceContextAccessor spaceContextAccessor,
            ISpaceManagementService spaceManagement
        )
        {
            _currentUser = currentUser;
            _membershipStore = membershipStore;
            _spaceContextAccessor = spaceContextAccessor;
            _spaceManagement = spaceManagement;
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
        /// 建立新空間，你是它的擁有者（owner）。只接受登入，應用程式權杖不能建立空間；每人最多擁有 10 個空間
        /// </summary>
        [HttpPost]
        [Authorize]
        [EnableRateLimiting(SpaceManagementRateLimits.Create)]
        [ProducesResponseType(typeof(SpaceVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] SaveSpaceParam request)
        {
            var (space, membership) = await _spaceManagement.CreateAsync(_currentUser.UserId, request.Name, HttpContext.RequestAborted);
            return Ok(ToVM(space, membership));
        }

        /// <summary>
        /// 重新命名空間（只有擁有者可以）
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(SpaceVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Rename([FromRoute] int id, [FromBody] SaveSpaceParam request)
        {
            var (space, membership) = await _spaceManagement.RenameAsync(_currentUser.UserId, id, request.Name, HttpContext.RequestAborted);
            return Ok(ToVM(space, membership));
        }

        private static SpaceVM ToVM(Models.Entities.Space space, Models.Entities.AccountSpace membership) =>
            new() { SpaceID = space.ID, Key = space.Key, Name = space.Name, Role = membership.Role, IsDefault = membership.IsDefault };

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
