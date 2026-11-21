using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchToReactivePackageCodeFixProvider)), Shared]
public sealed class SwitchToReactivePackageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ProxyDomainTable.ReactivePackageByDiagnosticId.Keys.ToImmutableArray();

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null
            || !ProxyDomainTable.ReactivePackageByDiagnosticId.TryGetValue(diagnostic.Id, out var packageId)
            || !ProxyDomainTable.TryGetDomainByBackendPackageId(packageId, out var domain))
        {
            return;
        }

        var project = context.Document.Project;
        var siblings = domain.BackendPackageIds.Where(id => id != packageId).ToArray();
        var add = await ProjectFileWriter.TryCreateActionAsync(
            project,
            $"Add package reference to '{packageId}'",
            $"{nameof(SwitchToReactivePackageCodeFixProvider)}:Add:{packageId}",
            content => !siblings.Any(id => CsprojPackageReferenceEditor.ContainsPackageReference(content, id))
                && PackageReferenceVersion.TryResolve(project, content, packageId, out var version)
                ? CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(content, packageId, version)
                : content,
            context.CancellationToken).ConfigureAwait(false);
        if (add is not null)
            context.RegisterCodeFix(add, diagnostic);

        var replace = await ProjectFileWriter.TryCreateActionAsync(
            project,
            $"Switch project to '{packageId}' package",
            $"{nameof(SwitchToReactivePackageCodeFixProvider)}:Switch:{packageId}",
            content => PackageReferenceVersion.TryResolve(project, content, packageId, out var version)
                ? CsprojPackageReferenceEditor.ReplacePackageReferences(content, siblings, packageId, version)
                : content,
            context.CancellationToken).ConfigureAwait(false);
        if (replace is not null)
            context.RegisterCodeFix(replace, diagnostic);
    }
}
