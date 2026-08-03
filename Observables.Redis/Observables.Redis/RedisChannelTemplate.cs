namespace Observables.Redis;

/// <summary>Resolves Channel templates with <c>{parameter}</c> placeholders.</summary>
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

        var result = template;
        foreach (var (name, value) in parameters)
        {
            result = result.Replace("{" + name + "}", value ?? string.Empty);
        }

        return result;
    }
}
