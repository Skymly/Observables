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
}
