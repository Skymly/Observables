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

        if (!ProxyDomainTable.TryGetByMissingRuntimeDiagnosticId(diagnostic.Id, out var domain))
            return Task.CompletedTask;

        foreach (var backend in ProxyDomainTable.InstallableBackendKinds)
        {
            RegisterPackageFix(context, diagnostic, domain.BackendPackageName(backend));
        }

        return Task.CompletedTask;
    }

    static void RegisterPackageFix(CodeFixContext context, Diagnostic diagnostic, string packageId)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add package reference to '{packageId}'",
                createChangedSolution: cancellationToken =>
                    AddPackageReferenceAsync(context.Document.Project, packageId, version: null, cancellationToken),
                equivalenceKey: $"{nameof(AddRuntimePackageReferenceCodeFixProvider)}:{packageId}"),
            diagnostic);
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
        foreach (var backendPackageId in ProxyDomainTable.AllBackendPackageIds)
        {
            if (!backendPackageId.StartsWith(runtimePackageId + ".", StringComparison.Ordinal))
                continue;

            var version = CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, backendPackageId);
            if (version is not null)
                return version;
        }

        return null;
    }
}
