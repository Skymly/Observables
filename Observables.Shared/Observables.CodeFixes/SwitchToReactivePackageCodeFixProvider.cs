using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Observables.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchToReactivePackageCodeFixProvider)), Shared]
public sealed class SwitchToReactivePackageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ObservablesPackageReferenceMappings.ReactivePackageByDiagnosticId.Keys.ToImmutableArray();

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
            return Task.CompletedTask;

        if (!ObservablesPackageReferenceMappings.TryGetReactivePackage(
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

        if (ObservablesPackageReferenceMappings.R3PackageByReactivePackageId.TryGetValue(
                reactivePackageId,
                out var r3PackageId))
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Switch to '{reactivePackageId}' package",
                    createChangedSolution: cancellationToken =>
                        SwitchToReactivePackageAsync(
                            context.Document.Project,
                            r3PackageId,
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
        string r3PackageId,
        string reactivePackageId,
        CancellationToken cancellationToken)
    {
        return ProjectFileWriter.ApplyProjectFileTransformAsync(
            project.Solution,
            project,
            content =>
            {
                var version = CsprojPackageReferenceEditor.TryGetPackageVersion(content, r3PackageId)
                    ?? InferReactivePackageVersion(content, reactivePackageId);
                return CsprojPackageReferenceEditor.ReplacePackageReference(
                    content,
                    r3PackageId,
                    reactivePackageId,
                    version);
            },
            cancellationToken);
    }

    internal static string? InferReactivePackageVersion(string csprojContent, string reactivePackageId)
    {
        if (ObservablesPackageReferenceMappings.R3PackageByReactivePackageId.TryGetValue(
                reactivePackageId,
                out var r3PackageId))
        {
            var version = CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, r3PackageId);
            if (version is not null)
                return version;
        }

        return CsprojPackageReferenceEditor.TryGetPackageVersion(csprojContent, reactivePackageId);
    }
}
