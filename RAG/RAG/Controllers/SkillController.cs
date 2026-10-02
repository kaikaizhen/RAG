using Asp.Versioning;
using Library.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Context;
using RAG.Models.ViewModels;
using RAG.Models.ViewModels.Context;
using RAG.Models.ViewModels.Skill;
using RAG.SkillRuntime;
using RAG.SpaceRuntime;
using RAG.Spaces;
using System.Net;

namespace RAG.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [RequireSpaceContext]
    [Tags("Skill / Workflow 功能模組")]
    public class SkillController : ControllerBase
    {
        private readonly ISpaceContextAccessor _spaceContextAccessor;
        private readonly ISkillResolver _skillResolver;
        private readonly ISkillBundleLoader _bundleLoader;
        private readonly ContextRuntimeOptions _contextOptions;

        public SkillController(
            ISpaceContextAccessor spaceContextAccessor,
            ISkillResolver skillResolver,
            ISkillBundleLoader bundleLoader,
            ContextRuntimeOptions contextOptions
        )
        {
            _spaceContextAccessor = spaceContextAccessor;
            _skillResolver = skillResolver;
            _bundleLoader = bundleLoader;
            _contextOptions = contextOptions;
        }

        /// <summary>
        /// 取得目前 Space 的有效 Skill / Workflow 內容（受 token 預算限制，Workflow 優先）
        /// </summary>
        /// <param name="tokenBudget">token 預算（預設與上限由伺服器設定）</param>
        /// <returns></returns>
        [HttpGet("bundle")]
        [ProducesResponseType(typeof(SkillBundleVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetBundle([FromQuery] int? tokenBudget)
        {
            var budget = tokenBudget ?? _contextOptions.DefaultTokenBudget;
            if (budget <= 0 || budget > _contextOptions.MaxTokenBudget)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"tokenBudget must be 1-{_contextOptions.MaxTokenBudget}");
            }

            var bundle = _skillResolver.Resolve(_spaceContextAccessor.Current)
                ?? throw new HttpException(HttpStatusCode.ServiceUnavailable, "Skill runtime is not configured");
            var loaded = await _bundleLoader.LoadAsync(bundle, budget, HttpContext.RequestAborted);

            return Ok(new SkillBundleVM
            {
                SpaceKey = bundle.SpaceKey,
                Skills = loaded.Skills.Select(ToLoadedVM).ToList(),
                Workflow = loaded.Workflow is null ? null : ToLoadedVM(loaded.Workflow),
                OmittedSkillIds = loaded.OmittedSkillIds.ToList(),
                TokenEstimate = loaded.TokenEstimate,
                TokenBudget = budget
            });
        }

        /// <summary>
        /// 查詢目前 Space 的有效 Skill / Workflow（僅 metadata，不含內容）
        /// </summary>
        /// <returns></returns>
        [HttpGet("effective")]
        [ProducesResponseType(typeof(EffectiveSkillVM), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorVM), StatusCodes.Status503ServiceUnavailable)]
        public IActionResult GetEffective()
        {
            var bundle = _skillResolver.Resolve(_spaceContextAccessor.Current)
                ?? throw new HttpException(HttpStatusCode.ServiceUnavailable, "Skill runtime is not configured");

            return Ok(new EffectiveSkillVM
            {
                SpaceKey = bundle.SpaceKey,
                Skills = bundle.Skills.Select(ToVM).ToList(),
                Workflow = bundle.Workflow is null ? null : ToVM(bundle.Workflow),
                TokenEstimate = bundle.TokenEstimate
            });
        }

        private static LoadedArtifactVM ToLoadedVM(LoadedArtifact artifact) => new()
        {
            Id = artifact.Id,
            Version = artifact.Version,
            Source = artifact.Source,
            Content = artifact.Content,
            TokenEstimate = artifact.TokenEstimate
        };

        private static ArtifactVM ToVM(ResolvedArtifact artifact) => new()
        {
            Id = artifact.Id,
            Version = artifact.Version,
            Name = artifact.Name,
            Source = artifact.Source,
            TokenEstimate = artifact.TokenEstimate
        };
    }
}
