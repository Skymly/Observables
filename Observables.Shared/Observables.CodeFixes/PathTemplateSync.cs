namespace Observables.CodeFixes;

internal static class PathTemplateSync
{
    internal static string SyncPathWithParameters(string path, IReadOnlyCollection<string> parameterNames)
    {
        var placeholders = ExtractPathPlaceholders(path);
        var desired = new HashSet<string>(parameterNames, StringComparer.Ordinal);

        foreach (var extra in placeholders.Where(p => !desired.Contains(p)).ToArray())
        {
            path = RemovePlaceholder(path, extra);
            placeholders.Remove(extra);
        }

        foreach (var missing in desired.Where(p => !placeholders.Contains(p)))
        {
            path = AppendPlaceholder(path, missing);
            placeholders.Add(missing);
        }

        return path;
    }

    internal static HashSet<string> ExtractPathPlaceholders(string path)
    {
        var placeholders = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '{')
            {
                continue;
            }

            var end = path.IndexOf('}', i + 1);
            if (end < 0)
            {
                break;
            }

            var name = path.Substring(i + 1, end - i - 1);
            if (name.Length > 0)
            {
                placeholders.Add(name);
            }

            i = end;
        }

        return placeholders;
    }

    static string RemovePlaceholder(string path, string placeholder)
    {
        var token = "{" + placeholder + "}";
        var index = path.IndexOf(token, StringComparison.Ordinal);
        if (index < 0)
        {
            return path;
        }

        var before = path.Substring(0, index);
        var after = path.Substring(index + token.Length);

        if (before.EndsWith("/", StringComparison.Ordinal))
        {
            before = before.Substring(0, before.Length - 1);
        }

        if (after.StartsWith("/", StringComparison.Ordinal))
        {
            after = after.Substring(1);
        }

        return before + after;
    }

    static string AppendPlaceholder(string path, string placeholder)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "{" + placeholder + "}";
        }

        if (!path.EndsWith("/", StringComparison.Ordinal))
        {
            path += '/';
        }

        return path + "{" + placeholder + "}";
    }
}
