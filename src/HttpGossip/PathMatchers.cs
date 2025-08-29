namespace HttpGossip
{
    internal static class PathMatchers
    {
        internal static bool MatchesAny(string path, string[]? patterns)
        {
            if (patterns == null || patterns.Length == 0) return false;
            var p = path ?? string.Empty;
            foreach (var pat in patterns)
            {
                if (string.IsNullOrWhiteSpace(pat)) continue;
                if (p.IndexOf(pat, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}