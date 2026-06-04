using System.Text;

namespace Observables.Mqtt;

/// <summary>Resolves topic templates with <c>{parameter}</c> placeholders.</summary>
public static class MqttTopic
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
            result = result.Replace(
                "{" + name + "}",
                Uri.EscapeDataString(value ?? string.Empty));
        }

        return result;
    }

    internal static string Format(string template, IReadOnlyList<string> parameterNames, object?[] argumentValues)
    {
        if (parameterNames.Count != argumentValues.Length)
        {
            throw new ArgumentException("Parameter name and value counts must match.");
        }

        var pairs = new (string Name, string? Value)[parameterNames.Count];
        for (var i = 0; i < parameterNames.Count; i++)
        {
            pairs[i] = (parameterNames[i], argumentValues[i]?.ToString());
        }

        return Format(template, pairs);
    }
}
