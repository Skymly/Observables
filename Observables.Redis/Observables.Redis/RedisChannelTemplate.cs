namespace Observables.Redis;

/// <summary>Resolves Channel templates with <c>{parameter}</c> placeholders.</summary>
/// <remarks>
/// Replacement is a single pass over the original template. Placeholder values are spliced
/// as literals and are not scanned for further <c>{name}</c> tokens.
/// </remarks>
public static class RedisChannelTemplate
{
    public static string Format(string template, params (string Name, string? Value)[] parameters)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (parameters is null || parameters.Length == 0)
        {
            return template;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in parameters)
        {
            map[name] = value ?? string.Empty;
        }

        var result = new System.Text.StringBuilder(template.Length);
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                result.Append(template[i]);
                continue;
            }

            var close = template.IndexOf('}', i + 1);
            if (close < 0)
            {
                result.Append(template, i, template.Length - i);
                break;
            }

            var name = template.Substring(i + 1, close - i - 1);
            if (map.TryGetValue(name, out var replacement))
            {
                result.Append(replacement);
                i = close;
                continue;
            }

            result.Append(template[i]);
        }

        return result.ToString();
    }
}
