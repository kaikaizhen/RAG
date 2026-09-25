using System.Text.RegularExpressions;

namespace RAG.ToolRuntime
{
    public enum CapabilityKind
    {
        /// <summary>
        /// 只讀取資料，無外部副作用
        /// </summary>
        Read,

        /// <summary>
        /// 會產生外部副作用；需額外的 approval policy（本階段未開放任何 Write capability）
        /// </summary>
        Write
    }

    public enum ToolArgumentType
    {
        String,
        Integer,
        Boolean
    }

    public sealed record ToolArgument(string Name, ToolArgumentType Type, bool Required, int MaxLength = 256);

    /// <summary>
    /// 固定 adapter 的能力宣告：只能以 capability ID 被選用，不接受任意指令
    /// </summary>
    public sealed record CapabilityDefinition(
        string Id,
        string Description,
        CapabilityKind Kind,
        string RequiredPermission,
        TimeSpan Timeout,
        IReadOnlyList<ToolArgument> Arguments);

    public static class CapabilityIds
    {
        // 例：knowledge.search；不可包含空白、路徑或 shell 字元
        private static readonly Regex Pattern = new("^[a-z][a-z0-9-]{0,31}(\\.[a-z][a-z0-9-]{0,31}){1,3}$", RegexOptions.CultureInvariant);

        public static bool IsValid(string? id) => id is not null && Pattern.IsMatch(id);
    }
}
