using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRuntimePackageReferenceCodeFixProvider)), Shared]
public sealed class AddRuntimePackageReferenceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ProxyDomainTable.RuntimePackageByDiagnosticId.Keys.ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
            return Task.CompletedTask;

        if (!ProxyDomainTable.RuntimePackageByDiagnosticId.TryGetValue(
                diagnostic.Id,
                out var packageId))
            return Task.CompletedTask;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add package reference to '{packageId}'",
                createChangedSolution: cancellationToken =>
                    AddPackageReferenceAsync(context.Document.Project, packageId, version: null, cancellationToken),
                equivalenceKey: $"{nameof(AddRuntimePackageReferenceCodeFixProvider)}:{packageId}"),
            diagnostic);

        return Task.CompletedTask;
    }

    internal static Task<Solution> AddPackageReferenceAsync(
        Project project,
        string packageId,
        string? version,
        CancellationToken cancellationToken)
    {
        return ProjectFileWriter.ApplyProjectFileTransformAsync(
            project.Solution,
            project,
            content =>
            {
                var inferredVersion = version
                    ?? InferVersionFromSiblingPackages(content, packageId);
                return CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(
                    content,
                    packageId,
                    inferredVersion);
            },
            cancellationToken);
    }

    internal static string? InferVersionFromSiblingPackages(string csprojContent, string runtimePackageId)
    {
        foreach (var pair in ProxyDomainTable.R3PackageByReactivePackageId)
        {
            if (!pair.Value.StartsWith(runtimePackageId + ".", StringComparison.Ordinal))
                continue;

            var version = CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, pair.Value);
            if (version is not null)
                return version;
        }

        foreach (var reactivePackage in ProxyDomainTable.ReactivePackageByDiagnosticId.Values)
        {
            if (!reactivePackage.StartsWith(runtimePackageId + ".", StringComparison.Ordinal))
                continue;

            var version = CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, reactivePackage);
            if (version is not null)
                return version;
        }

        return null;
    }
}
