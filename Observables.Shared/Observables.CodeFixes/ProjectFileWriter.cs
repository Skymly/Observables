using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace Observables.CodeFixes;

internal static class ProjectFileWriter
{
    internal static async Task<CodeAction?> TryCreateActionAsync(
        Project project,
        string title,
        string equivalenceKey,
        Func<string, string> transform,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (project.FilePath is not { Length: > 0 } path)
            return null;

        var solution = project.Solution;
        var workspace = solution.Workspace;
        foreach (var id in solution.GetDocumentIdsWithFilePath(path))
        {
            if (id.ProjectId != project.Id)
                continue;

            // A project file must already be tracked and editable by this host.
            // Adding an additional document would only produce an unsupported project edit.
            var additional = solution.GetAdditionalDocument(id);
            var document = (TextDocument?)additional ?? solution.GetDocument(id);
            if (document is null || !workspace.CanApplyChange(additional is null
                    ? ApplyChangesKind.ChangeDocument : ApplyChangesKind.ChangeAdditionalDocument))
            {
                continue;
            }

            var original = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var originalValue = original.ToString();
            string updated;
            try
            {
                updated = transform(originalValue);
            }
            catch (XmlException)
            {
                // A partially edited project or props file is not ready for a package change.
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            if (string.Equals(originalValue, updated, StringComparison.Ordinal))
                return null;

            var text = SourceText.From(updated, original.Encoding ?? Encoding.UTF8, original.ChecksumAlgorithm);
            return CodeAction.Create(
                title,
                createChangedSolution: _ => Task.FromResult(additional is null
                    ? solution.WithDocumentText(id, text)
                    : solution.WithAdditionalDocumentText(id, text)),
                equivalenceKey);
        }

        return null;
    }
}
