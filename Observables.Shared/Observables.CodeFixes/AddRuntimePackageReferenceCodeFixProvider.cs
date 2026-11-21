using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRuntimePackageReferenceCodeFixProvider)), Shared]
public sealed class AddRuntimePackageReferenceCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ProxyDomainTable.RuntimePackageByDiagnosticId.Keys.ToImmutableArray();

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null
            || !ProxyDomainTable.TryGetByMissingRuntimeDiagnosticId(diagnostic.Id, out var domain))
        {
            return;
        }

        var project = context.Document.Project;
        foreach (var backend in ProxyDomainTable.InstallableBackendKinds)
        {
            var packageId = domain.BackendPackageName(backend);
            var action = await ProjectFileWriter.TryCreateActionAsync(
                project,
                $"Add package reference to '{packageId}'",
                $"{nameof(AddRuntimePackageReferenceCodeFixProvider)}:{packageId}",
                content =>
                {
                    if (domain.BackendPackageIds.Any(id =>
                            CsprojPackageReferenceEditor.ContainsPackageReference(content, id))
                        || !PackageReferenceVersion.TryResolve(project, content, packageId, out var version))
                    {
                        return content;
                    }

                    return CsprojPackageReferenceEditor.AddPackageReferenceIfMissing(content, packageId, version);
                },
                context.CancellationToken).ConfigureAwait(false);

            if (action is not null)
                context.RegisterCodeFix(action, diagnostic);
        }
    }
}
