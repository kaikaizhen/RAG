namespace RAG.SkillRuntime
{
    /// <summary>
    /// 受控檔案存取：只允許 baseDirectory 之內、非 symlink / junction 的一般檔案
    /// </summary>
    public static class ControlledPath
    {
        public static bool TryResolveFile(string baseDirectory, string relativePath, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            {
                error = $"'{relativePath}' must be a relative path";
                return false;
            }

            var segments = relativePath.Split('/', '\\');
            if (segments.Any(s => s is "" or "." or ".."))
            {
                error = $"'{relativePath}' contains an empty, '.' or '..' segment";
                return false;
            }

            var baseFull = EnsureTrailingSeparator(Path.GetFullPath(baseDirectory));
            var candidate = Path.GetFullPath(Path.Combine(baseFull, relativePath));
            if (!candidate.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
            {
                error = $"'{relativePath}' escapes its controlled directory";
                return false;
            }

            if (!File.Exists(candidate))
            {
                error = $"'{relativePath}' does not exist";
                return false;
            }

            // 從檔案一路往上到 baseDirectory，任何一層為 reparse point（symlink / junction）即拒絕
            for (var current = candidate; current.Length >= baseFull.Length - 1; current = Path.GetDirectoryName(current)!)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    error = $"'{relativePath}' passes through a symbolic link";
                    return false;
                }
                if (string.Equals(EnsureTrailingSeparator(current), baseFull, StringComparison.OrdinalIgnoreCase)) break;
            }

            fullPath = candidate;
            return true;
        }

        public static string EnsureTrailingSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }
}
