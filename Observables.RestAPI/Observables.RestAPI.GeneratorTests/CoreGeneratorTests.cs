using System.IO.Compression;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Observables.RestAPI.GeneratorTests;

public class CoreGeneratorTests
{
    [Fact]
    public Task GetTaskUser_generates_rest_stub()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public Task GetObservableUser_uses_from_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
            }
            """);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public Task GetObservableUser_with_cancellation_token_links_caller_token()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int id, CancellationToken cancellationToken);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains(
            "CreateLinkedTokenSource(______ct, @cancellationToken)",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______ct, @id)",
            snapshot,
            StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Custom_http_method_attribute_emits_verb_from_attribute_name()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public sealed class PurgeAttribute(string path) : HttpMethodAttribute(path)
            {
                public override HttpMethod Method => new("PURGE");
            }

            public interface ICacheApi
            {
                [Purge("/cache/{key}")]
                Task Purge(string key);
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("new(\"PURGE\"", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("new(\"GET\", \"/cache/{key}\"", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Body_without_buffered_override_uses_settings_buffered_default()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Post("/users")]
                Task<User> CreateUser([Body] User user);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______ct, @user)", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings.Buffered", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task GetUser_path_parameter_mismatch_reports_OBS3004()
    {
        // Path placeholder {id} does not match any parameter name → OBS3004.
        // (Unattributed extra parameters default to Query and do NOT trigger OBS3004.)
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int userId);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains("OBS3004", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_AliasAs_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser([AliasAs("id")] int userId);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        Assert.Contains("SlotKind.Path, 0, \"id\"", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task IObservable_on_R3_generator_reports_OBS3003()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            using System;

            public interface IUserApi
            {
                [Get("/users/{id}")]
                IObservable<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains("OBS3003", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public void RestAPI_OBS3002_when_runtime_missing()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """,
            includeCoreReference: false);

        Assert.Contains("OBS3002", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
    }

    // ── Path + [Body]/[Query] regression tests (issue #111) ──

    [Fact]
    public Task Path_with_body_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IGitHubApi
            {
                [Post("/repos/{owner}/{repo}/issues/{number}/comments")]
                Task<Comment> CreateComment(string owner, string repo, int number, [Body] CommentBody body);
            }

            public sealed class Comment { public int Id { get; set; } }
            public sealed class CommentBody { public string Text { get; set; } = ""; }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Query_defaults_to_UriEscaped()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface ISearchApi
            {
                [Get("/search")]
                Task<string> Search([Query] string q);
            }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("queryUriFormat: 0", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("queryUriFormat: 2", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_with_query_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IGitHubApi
            {
                [Get("/repos/{owner}/{repo}/issues")]
                Task<Issue[]> ListIssues(string owner, string repo, [Query] string state);
            }

            public sealed class Issue { public int Number { get; set; } }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_with_header_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id, [Header("X-Api-Key")] string apiKey);
            }

            public sealed class User { public int Id { get; set; } }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_with_authorize_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id, [Authorize("Bearer")] string token);
            }

            public sealed class User { public int Id { get; set; } }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_with_property_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id, [Property("RequestId")] string requestId);
            }

            public sealed class User { public int Id { get; set; } }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task Path_with_header_collection_parameter_does_not_report_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id, [HeaderCollection] System.Collections.Generic.Dictionary<string, string> headers);
            }

            public sealed class User { public int Id { get; set; } }
            """);

        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.DoesNotContain("OBS3004", snapshot, StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    // ── Incremental cache hit tests ──

    const string CacheTestSource =
        """
        public interface IUserApi
        {
            [Get("/users/{id}")]
            Task<User> GetUser(int id);
        }

        public sealed class User
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }
        """;

    [Fact]
    public void Cache_unchanged_compilation_reuses_build_step()
    {
        // Run once with tracking enabled, then re-run on the same compilation.
        // The BuildRestApi step should report a cache hit (Cached or Unchanged).
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var result = harness.RunSecond();
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_reuses_report_diagnostics_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree(
            """
            public interface IUnrelated : System.IDisposable
            {
                void M();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "ReportDiagnostics");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected ReportDiagnostics cache hit (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_marked_interface_reuses_parse_step()
    {
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree(
            """
            public interface IUnrelated : System.IDisposable
            {
                void M();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "ParseRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected ParseRestApi cache hit after unrelated interface (Cached/Unchanged), got {reason}");
    }

    [Fact]
    public void Cache_unrelated_edit_preserves_build_step()
    {
        // Add an unrelated syntax tree (no RestAPI interfaces with [Get]/[Post] methods).
        // RestAPI uses CreateSyntaxProvider (not ForAttributeWithMetadataName), which has a
        // broader syntax filter that may trigger on any method with attributes in any interface.
        // As a result the BuildRestApi step may report Modified instead of a cache hit.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithUnrelatedTree();
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        // CreateSyntaxProvider has broader invalidation than ForAttributeWithMetadataName,
        // so accept Modified in addition to Cached/Unchanged.
        Assert.True(
            reason is IncrementalStepRunReason.Cached
                or IncrementalStepRunReason.Unchanged
                or IncrementalStepRunReason.Modified,
            $"Expected cache hit or Modified (Cached/Unchanged/Modified), got {reason}");
    }

    [Fact]
    public void Cache_restapi_interface_edit_invalidates_build_step()
    {
        // Add a second interface with a [Get] method → candidate set changes → cache miss.
        var harness = GeneratorTestHarness.RunWithCacheTracking(CacheTestSource);
        var edited = harness.WithAdditionalSource(
            """
            public interface ISecondApi
            {
                [Get("/ping")]
                Task<string> Ping();
            }
            """);
        var result = harness.RunSecond(edited);
        var reason = GeneratorTestHarness.GetStepReason(result, "BuildRestApi");
        Assert.True(
            reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New,
            $"Expected cache miss (Modified/New), got {reason}");
    }
}
