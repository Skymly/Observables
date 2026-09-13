using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Observables.CodeFixes;

internal static class ProjectFileWriter
{
    public static async Task<Solution> ApplyProjectFileTransformAsync(
        Solution solution,
        Project project,
        Func<string, string> transform,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (project.FilePath is not { Length: > 0 } path)
            return solution;

        var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
        string original;
        SourceText? originalText = null;

        if (documentId is not null)
        {
            var textDocument = (TextDocument?)solution.GetAdditionalDocument(documentId)
                ?? solution.GetDocument(documentId);
            if (textDocument is not null)
            {
                originalText = await textDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                original = originalText.ToString();
            }
            else if (!File.Exists(path))
            {
                return solution;
            }
            else
            {
                original = File.ReadAllText(path);
            }
        }
        else if (File.Exists(path))
        {
            original = File.ReadAllText(path);
        }
        else
        {
            return solution;
        }

        var updated = transform(original);
        if (string.Equals(original, updated, StringComparison.Ordinal))
            return solution;

        var newText = SourceText.From(updated, originalText?.Encoding ?? Encoding.UTF8);

        if (documentId is not null)
        {
            if (solution.GetAdditionalDocument(documentId) is not null)
                return solution.WithAdditionalDocumentText(documentId, newText);

            if (solution.GetDocument(documentId) is not null)
                return solution.WithDocumentText(documentId, newText);
        }

        return solution.AddAdditionalDocument(
            DocumentId.CreateNewId(project.Id),
            Path.GetFileName(path),
            newText,
            filePath: path);
    }
}
