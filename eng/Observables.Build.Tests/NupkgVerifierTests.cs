namespace Observables.Build.Tests;

public sealed class NupkgVerifierTests
{
    const string EventsR3 = "Observables.Events.R3";
    const string EventsGenerator = "Observables.Events.R3.SourceGenerators.dll";
    const string GrpcR3 = "Observables.Grpc.R3";
    const string GrpcGenerator = "Observables.Grpc.R3.SourceGenerators.dll";

    static readonly string[] SharedAnalyzerFiles =
    [
        "analyzers/dotnet/roslyn4.12/cs/Observables.CodeFixes.dll",
        "analyzers/dotnet/roslyn4.12/cs/Observables.Analyzers.dll",
        "README.md",
    ];

    [Fact]
    public void Verify_fails_when_events_package_only_has_legacy_observables_events_props()
    {
        string nupkg = NupkgFixture.Create(
            EventsR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{EventsGenerator}",
                .. SharedAnalyzerFiles,
                "buildTransitive/observables.events.props",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3"],
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = EventsR3,
                    GeneratorAssemblyFileName = EventsGenerator,
                });

            Assert.Contains(
                errors,
                static e => e.Contains("Observables.Events.R3.props", StringComparison.Ordinal));
            Assert.DoesNotContain(
                errors,
                static e => e.Contains("observables.events.props", StringComparison.OrdinalIgnoreCase)
                    && e.Contains("missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_fails_when_analyzer_folder_has_a_dll_but_not_the_domain_generator()
    {
        string nupkg = NupkgFixture.Create(
            EventsR3,
            [
                "analyzers/dotnet/roslyn4.12/cs/SomeOtherAnalyzer.dll",
                .. SharedAnalyzerFiles,
                $"build/Observables.Events.R3.props",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3"],
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = EventsR3,
                    GeneratorAssemblyFileName = EventsGenerator,
                });

            Assert.Contains(
                errors,
                static e => e.Contains(EventsGenerator, StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_fails_when_nuspec_omits_a_required_lib_package_reference()
    {
        string nupkg = NupkgFixture.Create(
            GrpcR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{GrpcGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Grpc.R3.props",
                "lib/netstandard2.0/Observables.Grpc.dll",
                "lib/net8.0/Observables.Grpc.dll",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3"],
                ["net8.0"] = ["R3"],
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = GrpcR3,
                    GeneratorAssemblyFileName = GrpcGenerator,
                    RequiredLibTfms = ["netstandard2.0", "net8.0"],
                    RequiredDependenciesByTfm = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["netstandard2.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                        ["net8.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                    },
                });

            Assert.Contains(
                errors,
                static e => e.Contains("Grpc.Core.Api", StringComparison.Ordinal));
            Assert.Contains(
                errors,
                static e => e.Contains("Google.Protobuf", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_fails_when_a_required_lib_tfm_folder_is_missing()
    {
        string nupkg = NupkgFixture.Create(
            GrpcR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{GrpcGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Grpc.R3.props",
                "lib/net8.0/Observables.Grpc.dll",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["net8.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = GrpcR3,
                    GeneratorAssemblyFileName = GrpcGenerator,
                    RequiredLibTfms = ["netstandard2.0", "net8.0"],
                    RequiredDependenciesByTfm = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["net8.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                    },
                });

            Assert.Contains(
                errors,
                static e => e.Contains("lib/netstandard2.0/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_accepts_a_package_with_named_generator_packageid_props_lib_tfms_and_nuspec_deps()
    {
        string nupkg = NupkgFixture.Create(
            GrpcR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{GrpcGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Grpc.R3.props",
                "buildTransitive/Observables.Grpc.R3.props",
                "lib/netstandard2.0/Observables.Grpc.dll",
                "lib/net8.0/Observables.Grpc.dll",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                ["net8.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = GrpcR3,
                    GeneratorAssemblyFileName = GrpcGenerator,
                    RequiredLibTfms = ["netstandard2.0", "net8.0"],
                    RequiredDependenciesByTfm = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["netstandard2.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                        ["net8.0"] = ["R3", "Grpc.Core.Api", "Google.Protobuf"],
                    },
                });

            Assert.Empty(errors);
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_fails_when_pack_readme_pins_observables_package_version()
    {
        string nupkg = NupkgFixture.Create(
            EventsR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{EventsGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Events.R3.props",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3"],
            },
            fileContents: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["README.md"] =
                    """
                    # Observables.Events.R3

                    ## Install

                    ```xml
                    <PackageReference Include="Observables.Events.R3" Version="0.1.2" />
                    <PackageReference Include="R3" Version="1.3.0" />
                    ```
                    """,
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = EventsR3,
                    GeneratorAssemblyFileName = EventsGenerator,
                });

            Assert.Contains(
                errors,
                static e => e.Contains("README.md", StringComparison.OrdinalIgnoreCase)
                    && e.Contains("Version", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_accepts_pack_readme_with_unpinned_observables_package_reference()
    {
        string nupkg = NupkgFixture.Create(
            EventsR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{EventsGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Events.R3.props",
            ],
            nuspecDependenciesByTfm: new Dictionary<string, IReadOnlyList<string>>
            {
                ["netstandard2.0"] = ["R3"],
            },
            fileContents: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["README.md"] =
                    """
                    # Observables.Events.R3

                    ## Install

                    ```xml
                    <PackageReference Include="Observables.Events.R3" />
                    <PackageReference Include="R3" Version="1.3.0" />
                    ```
                    """,
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = EventsR3,
                    GeneratorAssemblyFileName = EventsGenerator,
                });

            Assert.Empty(errors);
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_fails_when_a_lib_assembly_references_a_forbidden_backend()
    {
        string assemblyPath = typeof(NupkgVerifierTests).Assembly.Location;
        string[] references = ReadReferences(assemblyPath);
        Assert.NotEmpty(references);

        string nupkg = NupkgFixture.Create(
            GrpcR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{GrpcGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Grpc.R3.props",
                "lib/net8.0/Observables.Grpc.dll",
            ],
            fileSources: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lib/net8.0/Observables.Grpc.dll"] = assemblyPath,
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = GrpcR3,
                    GeneratorAssemblyFileName = GrpcGenerator,
                    ForbiddenLibAssemblyReferences = [references[0]],
                });

            Assert.Contains(
                errors,
                e => e.Contains("lib/net8.0/Observables.Grpc.dll", StringComparison.Ordinal)
                    && e.Contains(references[0], StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    [Fact]
    public void Verify_accepts_a_lib_assembly_that_references_neither_forbidden_backend()
    {
        string assemblyPath = typeof(NupkgVerifierTests).Assembly.Location;
        Assert.DoesNotContain("R3", ReadReferences(assemblyPath), StringComparer.OrdinalIgnoreCase);

        string nupkg = NupkgFixture.Create(
            GrpcR3,
            [
                $"analyzers/dotnet/roslyn4.12/cs/{GrpcGenerator}",
                .. SharedAnalyzerFiles,
                "build/Observables.Grpc.R3.props",
                "lib/net8.0/Observables.Grpc.dll",
            ],
            fileSources: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lib/net8.0/Observables.Grpc.dll"] = assemblyPath,
            });

        try
        {
            IReadOnlyList<string> errors = NupkgVerifier.Verify(
                nupkg,
                new NupkgVerifyRequest
                {
                    PackageId = GrpcR3,
                    GeneratorAssemblyFileName = GrpcGenerator,
                    ForbiddenLibAssemblyReferences = ["R3"],
                });

            Assert.Empty(errors);
        }
        finally
        {
            File.Delete(nupkg);
        }
    }

    static string[] ReadReferences(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        return NupkgVerifier.ReadAssemblyReferences(stream).ToArray();
    }

    [Fact]
    public void Pack_readme_sources_do_not_pin_observables_package_version()
    {
        string[] packReadmes = Directory.GetFiles(
            RepoRoot.Find(),
            "README.*.pack.md",
            SearchOption.AllDirectories);

        Assert.NotEmpty(packReadmes);

        foreach (string path in packReadmes)
        {
            string relative = Path.GetRelativePath(RepoRoot.Find(), path);
            Assert.False(
                NupkgVerifier.ReadmePinsObservablesPackageVersion(File.ReadAllText(path)),
                relative);
        }
    }
}
