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

    [Fact]
    public void RestApi_IApiResponse_uses_wrapper_type_arguments_without_flag()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<IApiResponse<User>> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(
            "SendAsync<global::Observables.RestAPI.IApiResponse<global::User>, global::User>(Client, ______request, _settings, false, ______ct)",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain(", true, ", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Foreign_IApiResponse_is_not_treated_as_rest_wrapper()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            namespace Other;

            public interface IApiResponse<T>
            {
                T Value { get; }
            }

            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<IApiResponse<User>> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(
            "SendAsync<global::Other.IApiResponse<global::Other.User>, global::Other.IApiResponse<global::Other.User>>",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SendAsync<global::Other.IApiResponse<global::Other.User>, global::Other.User>",
            snapshot,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NonHttp_IObservable_on_R3_is_classified_as_unsupported()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            using System;

            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id);

                IObservable<User> Watch();
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("OBS3001", snapshot, StringComparison.Ordinal);
        Assert.Contains("OBS3005", snapshot, StringComparison.Ordinal);
    }
}
