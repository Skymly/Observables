using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Observables.CodeFixes.Tests;

public sealed class AddRuntimePackageReferenceCodeFixProviderTests
{
    [Fact]
    public async Task RegisterCodeFixes_offers_only_published_backend_packages()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "Consumer",
            "Consumer",
            LanguageNames.CSharp));
        var document = project.AddDocument("Consumer.cs", SourceText.From(""));

        var diagnostic = Diagnostic.Create(
            "OBS4002",
            "Observables",
            "missing runtime package",
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            warningLevel: 1);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            TestContext.Current.CancellationToken);

        await new AddRuntimePackageReferenceCodeFixProvider().RegisterCodeFixesAsync(context);

        // The Primitives backend is modeled for OBS0001 but its package is not published
        // (ADR-005); an install fix must not produce a PackageReference that cannot restore.
        Assert.Equal(
            [
                "Add package reference to 'Observables.SignalR.R3'",
                "Add package reference to 'Observables.SignalR.Reactive'",
            ],
            actions.Select(static a => a.Title));
    }
}
