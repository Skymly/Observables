using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Observables.CodeFixes.Tests;

public sealed class ProjectFileWriterTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Package_fixes_require_an_existing_editable_project_document(bool trackFile, bool supportChanges)
    {
        var directory = Directory.CreateTempSubdirectory("obs-codefix-");
        try
        {
            var path = Path.Combine(directory.FullName, "Consumer.csproj");
            File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            using var workspace = new ProjectTestWorkspace(path, trackFile, supportChanges);
            var document = workspace.CurrentSolution.Projects.Single().Documents.Single();

            Assert.Empty(await GetActionsAsync(new AddRuntimePackageReferenceCodeFixProvider(), document, "OBS5002"));
            Assert.Empty(await GetActionsAsync(new SwitchToReactivePackageCodeFixProvider(), document, "OBS5005"));
            Assert.Equal("<Project Sdk=\"Microsoft.NET.Sdk\" />", File.ReadAllText(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Project_action_preview_leaves_disk_unchanged_and_apply_updates_all_targets()
    {
        const string projectText = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
              </PropertyGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                <PackageReference Include="Observables.Mqtt.R3" Version="0.4.0"/>
              </ItemGroup>
              <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
                <PackageReference Include="observables.mqtt.r3" Version="0.4.1"/>
              </ItemGroup>
            </Project>
            """;
        var directory = Directory.CreateTempSubdirectory("obs-codefix-");
        try
        {
            var path = Path.Combine(directory.FullName, "Consumer.csproj");
            File.WriteAllText(path, projectText);
            using var workspace = new ProjectTestWorkspace(path);
            var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
            var token = TestContext.Current.CancellationToken;
            var action = Assert.Single(await GetActionsAsync(new SwitchToReactivePackageCodeFixProvider(), document, "OBS5005"));

            Assert.Equal("Switch project to 'Observables.Mqtt.Reactive' package", action.Title);
            _ = await action.GetPreviewOperationsAsync(token);
            var operations = await action.GetOperationsAsync(token);
            var change = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations));
            Assert.Equal(projectText, File.ReadAllText(path));
            Assert.Single(change.ChangedSolution.Projects.Single().AdditionalDocumentIds);

            Assert.True(workspace.TryApplyChanges(change.ChangedSolution));

            var updated = File.ReadAllText(path);
            Assert.Contains("Include=\"Observables.Mqtt.Reactive\" Version=\"0.4.0\"", updated, StringComparison.Ordinal);
            Assert.Contains("Include=\"Observables.Mqtt.Reactive\" Version=\"0.4.1\"", updated, StringComparison.Ordinal);
            Assert.DoesNotContain("Observables.Mqtt.R3", updated, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    internal static async Task<List<CodeAction>> GetActionsAsync(CodeFixProvider provider, Document document, string diagnosticId)
    {
        var diagnostic = Diagnostic.Create(
            diagnosticId,
            "Observables",
            "missing package",
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
        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    internal sealed class ProjectTestWorkspace : Workspace
    {
        readonly bool supportChanges;

        internal ProjectTestWorkspace(string path, bool trackFile = true, bool supportChanges = true)
            : base(MefHostServices.DefaultHost, "ProjectFileTest")
        {
            this.supportChanges = supportChanges;
            var projectId = ProjectId.CreateNewId();
            var source = DocumentInfo.Create(DocumentId.CreateNewId(projectId), "Consumer.cs",
                loader: CreateLoader(""));
            var projectFile = DocumentInfo.Create(DocumentId.CreateNewId(projectId), Path.GetFileName(path),
                loader: CreateLoader(File.ReadAllText(path)), filePath: path);
            var project = ProjectInfo.Create(projectId, VersionStamp.Create(), "Consumer", "Consumer",
                LanguageNames.CSharp, filePath: path, documents: [source],
                additionalDocuments: trackFile ? [projectFile] : []);
            OnSolutionAdded(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(), projects: [project]));
        }

        public override bool CanApplyChange(ApplyChangesKind feature) =>
            supportChanges && feature == ApplyChangesKind.ChangeAdditionalDocument;

        protected override void ApplyAdditionalDocumentTextChanged(DocumentId documentId, SourceText text)
        {
            var path = CurrentSolution.GetAdditionalDocument(documentId)!.FilePath!;
            File.WriteAllText(path, text.ToString(), text.Encoding ?? System.Text.Encoding.UTF8);
            base.ApplyAdditionalDocumentTextChanged(documentId, text);
        }

        static TextLoader CreateLoader(string text) =>
            TextLoader.From(TextAndVersion.Create(SourceText.From(text), VersionStamp.Create()));
    }
}
