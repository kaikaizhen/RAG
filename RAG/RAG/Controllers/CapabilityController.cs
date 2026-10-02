using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.SpaceRuntime;
using RAG.Spaces;
using RAG.ToolRuntime;

namespace RAG.Controllers
{
    /// <summary>
    /// 只提供目前 Space 可見的 capability 清單；工具執行僅限伺服器端 Agent Runtime 經 IToolInvoker 呼叫
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [RequireSpaceContext]
    [Tags("Tool 功能模組")]
    public class CapabilityController : ControllerBase
    {
        private readonly ISpaceContextAccessor _spaceContextAccessor;
        private readonly ICapabilityResolver _capabilityResolver;

        public CapabilityController(
            ISpaceContextAccessor spaceContextAccessor,
            ICapabilityResolver capabilityResolver
        )
        {
            _spaceContextAccessor = spaceContextAccessor;
            _capabilityResolver = capabilityResolver;
        }

        /// <summary>
        /// 查詢目前 Space 可使用的 capability
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Get()
        {
            var capabilities = _capabilityResolver.Resolve(_spaceContextAccessor.Current)
                .Select(c => new { c.Id, c.Description, Kind = c.Kind.ToString().ToLowerInvariant() })
                .ToList();

            return Ok(capabilities);
        }
    }
}
