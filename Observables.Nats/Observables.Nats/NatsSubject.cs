using System.Globalization;

namespace Observables.Nats;

/// <summary>Resolves subject templates with <c>{parameter}</c> placeholders.</summary>
/// <remarks>
/// Placeholder values are literal NATS tokens: they must be non-empty and must not contain
/// <c>.</c>, <c>*</c>, <c>&gt;</c>, or whitespace. Values are not URI-encoded, because encoding
/// would not keep a dotted value as a single token.
/// </remarks>
public static class NatsSubject
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
            var token = value ?? string.Empty;
            if (!IsLiteralToken(token))
            {
                throw new ArgumentException(
                    "NATS placeholder '" + name + "' must be a single subject token and cannot contain '.', '*', '>', or whitespace.",
                    nameof(parameters));
            }

            result = result.Replace("{" + name + "}", token);
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
            pairs[i] = (
                parameterNames[i],
                argumentValues[i] is null
                    ? null
                    : Convert.ToString(argumentValues[i], CultureInfo.InvariantCulture));
        }

        return Format(template, pairs);
    }

    internal static bool IsLiteralToken(string token)
    {
        if (token.Length == 0)
        {
            return false;
        }

        foreach (var ch in token)
        {
            if (ch is '.' or '*' or '>' || char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return true;
    }
}
