using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchToReactivePackageCodeFixProvider)), Shared]
public sealed class SwitchToReactivePackageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ProxyDomainTable.ReactivePackageByDiagnosticId.Keys.ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
            return Task.CompletedTask;

        if (!ProxyDomainTable.ReactivePackageByDiagnosticId.TryGetValue(
                diagnostic.Id,
                out var reactivePackageId))
            return Task.CompletedTask;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add package reference to '{reactivePackageId}'",
                createChangedSolution: cancellationToken =>
                    AddReactivePackageAsync(context.Document.Project, reactivePackageId, cancellationToken),
                equivalenceKey: $"{nameof(SwitchToReactivePackageCodeFixProvider)}:Add:{reactivePackageId}"),
            diagnostic);

        if (ProxyDomainTable.TryGetDomainByBackendPackageId(reactivePackageId, out var domain))
        {
            var siblingPackageIds = domain.BackendPackageIds
                .Where(id => !string.Equals(id, reactivePackageId, StringComparison.Ordinal))
                .ToImmutableArray();
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Switch to '{reactivePackageId}' package",
                    createChangedSolution: cancellationToken =>
                        SwitchToReactivePackageAsync(
                            context.Document.Project,
                            siblingPackageIds,
                            reactivePackageId,
                            cancellationToken),
                    equivalenceKey: $"{nameof(SwitchToReactivePackageCodeFixProvider)}:Switch:{reactivePackageId}"),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    internal static Task<Solution> AddReactivePackageAsync(
        Project project,
        string reactivePackageId,
        CancellationToken cancellationToken)
    {
        return ProjectFileWriter.ApplyProjectFileTransformAsync(
            project.Solution,
            project,
            content =>
            {
                var version = InferReactivePackageVersion(content, reactivePackageId);
                return CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
                    content,
                    reactivePackageId,
                    version);
            },
            cancellationToken);
    }

    internal static Task<Solution> SwitchToReactivePackageAsync(
        Project project,
        IReadOnlyList<string> removedPackageIds,
        string reactivePackageId,
        CancellationToken cancellationToken)
    {
        return ProjectFileWriter.ApplyProjectFileTransformAsync(
            project.Solution,
            project,
            content =>
            {
                // Keep the reactive package, drop every other backend package for the domain:
                // rename the first present sibling in place (preserves Version), remove the rest.
                var version = InferReactivePackageVersion(content, reactivePackageId);
                var updated = content;
                var switched = false;
                foreach (var removedPackageId in removedPackageIds)
                {
                    if (!switched
                        && CsprojPackageReferenceEditor.ContainsPackageReference(updated, removedPackageId))
                    {
                        updated = CsprojPackageReferenceEditor.ReplacePackageReference(
                            updated,
                            removedPackageId,
                            reactivePackageId,
                            version);
                        switched = true;
                    }
                    else
                    {
                        updated = CsprojPackageReferenceEditor.RemovePackageReference(updated, removedPackageId);
                    }
                }

                return switched
                    ? updated
                    : CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(updated, reactivePackageId, version);
            },
            cancellationToken);
    }

    internal static string? InferReactivePackageVersion(string csprojContent, string reactivePackageId)
    {
        if (ProxyDomainTable.TryGetDomainByBackendPackageId(reactivePackageId, out var domain))
        {
            foreach (var backendPackageId in domain.BackendPackageIds)
            {
                if (string.Equals(backendPackageId, reactivePackageId, StringComparison.Ordinal))
                    continue;

                var version = CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, backendPackageId);
                if (version is not null)
                    return version;
            }
        }

        return CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, reactivePackageId);
    }
}
