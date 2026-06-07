using Microsoft.CodeAnalysis;

namespace Observables.CodeFixes;

internal static class ProjectFileWriter
{
    public static Task<Solution> ApplyProjectFileTransformAsync(
        Solution solution,
        Project project,
        Func<string, string> transform,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (project.FilePath is not { Length: > 0 } path)
            return Task.FromResult(solution);

        var original = File.ReadAllText(path);
        var updated = transform(original);
        if (string.Equals(original, updated, StringComparison.Ordinal))
            return Task.FromResult(solution);

        File.WriteAllText(path, updated);
        return Task.FromResult(solution);
    }
}
