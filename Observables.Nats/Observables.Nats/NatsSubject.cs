using System.Globalization;
using System.Text;

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

        var bindings = new (string Placeholder, string Token)[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var (name, value) = parameters[i];
            var token = value ?? string.Empty;
            if (!IsLiteralToken(token))
            {
                throw new ArgumentException(
                    "NATS placeholder '" + name + "' must be a single subject token and cannot contain '.', '*', '>', or whitespace.",
                    nameof(parameters));
            }

            bindings[i] = ("{" + name + "}", token);
        }

        // Search the template only. Text inserted from one binding stays literal.
        StringBuilder? result = null;
        var copied = 0;
        for (var start = template.IndexOf('{'); start >= 0; start = template.IndexOf('{', start + 1))
        {
            var match = (Placeholder: string.Empty, Token: string.Empty);
            foreach (var binding in bindings)
            {
                var length = binding.Placeholder.Length;
                if (length > match.Placeholder.Length
                    && length <= template.Length - start
                    && string.CompareOrdinal(template, start, binding.Placeholder, 0, length) == 0)
                {
                    match = binding;
                }
            }

            if (match.Placeholder.Length == 0)
            {
                continue;
            }

            result ??= new StringBuilder(template.Length);
            result.Append(template, copied, start - copied).Append(match.Token);
            copied = start + match.Placeholder.Length;
            start = copied - 1;
        }

        return result is null
            ? template
            : result.Append(template, copied, template.Length - copied).ToString();
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
