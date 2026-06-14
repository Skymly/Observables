using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.Nats.Generators;

internal sealed class SourceWriter
{
    const char IndentationChar = ' ';
    const int CharsPerIndentation = 4;

    readonly StringBuilder sb = new();
    int indentation;

    public int Indentation
    {
        get => indentation;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            indentation = value;
        }
    }

    public void WriteLine(string text)
    {
        if (indentation == 0)
        {
            sb.AppendLine(text);
            return;
        }

        bool isFinalLine;
        ReadOnlySpan<char> remainingText = text.AsSpan();
        do
        {
            ReadOnlySpan<char> nextLine = GetNextLine(ref remainingText, out isFinalLine);

            if (nextLine.Length > 0)
            {
                AddIndentation();
            }

            AppendSpan(sb, nextLine);
            sb.AppendLine();
        }
        while (!isFinalLine);
    }

    public SourceText ToSourceText()
    {
        Debug.Assert(indentation == 0);
        return SourceText.From(sb.ToString(), Encoding.UTF8);
    }

    void AddIndentation() => sb.Append(IndentationChar, CharsPerIndentation * indentation);

    static ReadOnlySpan<char> GetNextLine(ref ReadOnlySpan<char> remainingText, out bool isFinalLine)
    {
        if (remainingText.Length == 0)
        {
            isFinalLine = true;
            return default;
        }

        ReadOnlySpan<char> next;
        ReadOnlySpan<char> rest;

        int lineLength = remainingText.IndexOf('\n');
        if (lineLength == -1)
        {
            lineLength = remainingText.Length;
            isFinalLine = true;
            rest = default;
        }
        else
        {
            rest = remainingText.Slice(lineLength + 1);
            isFinalLine = false;
        }

        if ((uint)lineLength > 0 && remainingText[lineLength - 1] == '\r')
        {
            lineLength--;
        }

        next = remainingText.Slice(0, lineLength);
        remainingText = rest;
        return next;
    }

    static void AppendSpan(StringBuilder builder, ReadOnlySpan<char> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            builder.Append(span[i]);
        }
    }
}
