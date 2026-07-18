using Microsoft.CodeAnalysis;

namespace Observables.RestAPI.GeneratorTests;

public sealed class BoundaryGeneratorTests
{
    [Fact]
    public void Nested_interface_generates_compilable_proxy()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            public static class ApiContainer
            {
                public interface IUserApi
                {
                    [Get("/users/{id}")]
                    Task<User> GetUser(int id);
                }
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.DoesNotContain(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains(
            output.GeneratedSources,
            static source => source.Source.Contains(
                "global::Demo.ApiContainer.IUserApi",
                StringComparison.Ordinal));
        Assert.Contains(
            output.GeneratedSources,
            static source => source.Source.Contains(
                "ApiContainerIUserApi",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Ref_struct_parameter_reports_generated_compilation_error()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            public ref struct RequestToken
            {
                public int Value;
            }

            public interface IUserApi
            {
                [Get("/users")]
                Task<User> GetUser(RequestToken token);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Ref_return_method_reports_generated_compilation_error()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Demo;

            public interface IUserApi
            {
                [Get("/users/{id}")]
                ref User GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains(
            output.Diagnostics,
            static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
