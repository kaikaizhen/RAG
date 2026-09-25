using RAG.Authentication;
using RAG.SpaceRuntime;
using System.Globalization;

namespace RAG.Spaces
{
    /// <summary>
    /// 對標記 <see cref="RequireSpaceContextAttribute"/> 的 endpoint 建立 ExecutionContext：
    /// 未驗證 → 401；hint 格式錯誤 → 400；未授權 / 無法決定 / 無 membership / resource selection 被拒 → 403。
    /// </summary>
    public class SpaceContextMiddleware
    {
        private readonly RequestDelegate _next;

        public SpaceContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IAccountSpaceResolver spaceResolver,
            IResourceScopeResolver resourceResolver,
            ISpaceContextAccessor accessor)
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<RequireSpaceContextAttribute>() is null)
            {
                await _next(context);
                return;
            }

            if (!CurrentUserAccessor.TryGetUserId(context.User, out var userId))
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
                return;
            }

            if (!TryReadSpaceHint(context.Request, out var spaceIdHint))
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid space selection");
                return;
            }

            var resolution = await spaceResolver.ResolveAsync(userId, spaceIdHint, context.RequestAborted);
            if (!resolution.Succeeded)
            {
                var message = resolution.Failure == SpaceResolutionFailure.SelectionRequired
                    ? "Space selection required"
                    : "Forbidden space";
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, message);
                return;
            }

            var resource = resourceResolver.Resolve(
                resolution.Context!,
                ReadSingleHeader(context.Request, SpaceSelectionHeaders.RepositoryId),
                ReadSingleHeader(context.Request, SpaceSelectionHeaders.ProjectId));
            if (!resource.Succeeded)
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "Forbidden resource scope");
                return;
            }

            accessor.Set(new SpaceExecutionContext(resolution.Context!, resource.Scope!));

            await _next(context);
        }

        private static bool TryReadSpaceHint(HttpRequest request, out int? spaceId)
        {
            spaceId = null;

            if (!request.Headers.TryGetValue(SpaceSelectionHeaders.SpaceId, out var values)) return true;
            if (values.Count != 1) return false;

            if (int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                spaceId = parsed;
                return true;
            }

            return false;
        }

        private static string? ReadSingleHeader(HttpRequest request, string name)
        {
            if (!request.Headers.TryGetValue(name, out var values)) return null;

            // 多值視為非空 hint，交由 resolver 拒絕
            return values.Count == 1 ? values[0] : string.Join(',', values.ToArray());
        }

        private static Task WriteErrorAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
