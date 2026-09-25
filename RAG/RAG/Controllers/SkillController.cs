using Asp.Versioning;
using Library.Http;
using Library.Http.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAG.Models.ViewModels;
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
        private readonly IApiExecutorHandler _apiExecutorHandler;
        private readonly ISpaceContextAccessor _spaceContextAccessor;
        private readonly ISkillResolver _skillResolver;

        public SkillController(
            IApiExecutorHandler apiExecutorHandler,
            ISpaceContextAccessor spaceContextAccessor,
            ISkillResolver skillResolver
        )
        {
            _apiExecutorHandler = apiExecutorHandler;
            _spaceContextAccessor = spaceContextAccessor;
            _skillResolver = skillResolver;
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
            return _apiExecutorHandler.Run(() =>
            {
                var bundle = _skillResolver.Resolve(_spaceContextAccessor.Current)
                    ?? throw new HttpException(HttpStatusCode.ServiceUnavailable, "Skill runtime is not configured");

                return new EffectiveSkillVM
                {
                    SpaceKey = bundle.SpaceKey,
                    Skills = bundle.Skills.Select(ToVM).ToList(),
                    Workflow = bundle.Workflow is null ? null : ToVM(bundle.Workflow),
                    TokenEstimate = bundle.TokenEstimate
                };
            },
            verify: false
            );
        }

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
