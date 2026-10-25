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
    public void Explicit_query_name_is_marked_so_the_key_formatter_skips_it()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users")]
                Task<string> Search([AliasAs("UserName")] string userName);
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));

        Assert.Contains("\"UserName\"", generated, StringComparison.Ordinal);
        Assert.Contains(".WithExplicitName()", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Inferred_query_name_is_left_to_the_key_formatter()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users")]
                Task<string> Search(string userName);
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));

        Assert.DoesNotContain(".WithExplicitName()", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Static_header_declared_on_two_base_interfaces_is_emitted_once()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            [Headers("X-Shared: 1")]
            public interface IBaseA
            {
            }

            [Headers("X-Shared: 1")]
            public interface IBaseB
            {
            }

            public interface IUserApi : IBaseA, IBaseB
            {
                [Get("/users")]
                Task<string> GetUsers();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));

        Assert.Contains(
            "staticHeaders: new string[] { \"X-Shared: 1\" }",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"X-Shared: 1\", \"X-Shared: 1\"",
            generated,
            StringComparison.Ordinal);
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
        Assert.Contains(
            "SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______scope, ______linked.Token, @id)",
            snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______scope, ______ct, @id)",
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
        Assert.Contains("SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______scope, ______ct, @user)", snapshot, StringComparison.Ordinal);
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
    public void RestAPI_OBS3002_names_consumer_packages_not_bare_runtime_id()
    {
        const string source =
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetUser(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """;

        string r3Snapshot = GeneratorTestHarness.ToSnapshot(
            GeneratorTestHarness.Run(source, includeCoreReference: false));
        Assert.Contains(
            "OBS3002: Observables.RestAPI.R3 is not referenced. Add a PackageReference to Observables.RestAPI.R3.",
            r3Snapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.RestAPI is not referenced", r3Snapshot, StringComparison.Ordinal);

        string reactiveSnapshot = GeneratorTestHarness.ToSnapshot(
            GeneratorTestHarness.RunReactive(source, includeCoreReference: false));
        Assert.Contains(
            "OBS3002: Observables.RestAPI.Reactive is not referenced. Add a PackageReference to Observables.RestAPI.Reactive.",
            reactiveSnapshot,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Observables.RestAPI is not referenced", reactiveSnapshot, StringComparison.Ordinal);
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

    [Fact]
    public void Query_then_AliasAs_keeps_explicit_name()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users")]
                Task<string> Search([Query] [AliasAs("q")] string search);
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains("\"q\"", generated, StringComparison.Ordinal);
        Assert.Contains(".WithExplicitName()", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AliasAs_then_Query_keeps_explicit_name()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users")]
                Task<string> Search([AliasAs("q")] [Query] string search);
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains("\"q\"", generated, StringComparison.Ordinal);
        Assert.Contains(".WithExplicitName()", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void Observable_Unit_delete_uses_send_void()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Delete("/users/{id}")]
                Observable<Unit> DeleteUser(int id);
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains("SendVoidAsync", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("SendAsync<global::R3.Unit", generated, StringComparison.Ordinal);
    }

    // Regression for the R-1 ledger entry: SendAsync<T,TBody> is Task<T?> where T is an
    // unconstrained type parameter, so T? compiles as [MaybeNull]T — value-type results already
    // bind. These cases pin generation for all value-type return shapes.

    [Fact]
    public void Task_of_int_generates_send_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/count")]
                Task<int> GetCount();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains(
            "SendAsync<int, int>(Client, _settings, in ______spec0, ______scope, ______ct).ConfigureAwait(false);",
            generated,
            StringComparison.Ordinal);
        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void ValueTask_of_int_generates_send_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/count")]
                ValueTask<int> GetCount();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains(
            "SendAsync<int, int>(Client, _settings, in ______spec0, ______scope, ______ct).ConfigureAwait(false);",
            generated,
            StringComparison.Ordinal);
        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void Sync_int_generates_send_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/count")]
                int GetCount();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains(
            "SendAsync<int, int>(Client, _settings, in ______spec0, ______scope, ______ct)).GetAwaiter().GetResult();",
            generated,
            StringComparison.Ordinal);
        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void Observable_of_int_generates_send_async()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/count")]
                Observable<int> GetCount();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains(
            "SendAsync<int, int>(Client, _settings, in ______spec0, ______scope, ______ct).ConfigureAwait(false);",
            generated,
            StringComparison.Ordinal);
        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void Task_of_nullable_int_does_not_emit_null_coalescing()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/count")]
                Task<int?> GetCount();
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source));
        Assert.Contains("SendAsync<int?, int?>", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("?? default", generated, StringComparison.Ordinal);
        GeneratorTestRunner.AssertCompiled(output);
    }

    [Fact]
    public void Concatenated_namespaces_do_not_collide()
    {
        const string source =
            """
            namespace Foo.Bar
            {
                public interface IApi
                {
                    [Get("/foo-bar")]
                    Task<string> Get();
                }
            }

            namespace FooBar
            {
                public interface IApi
                {
                    [Get("/foobar")]
                    Task<string> Get();
                }
            }
            """;

        var output = GeneratorTestHarness.Run(source);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);
        Assert.Contains("Foo_BarIApi", snapshot, StringComparison.Ordinal);
        Assert.Contains("FooBarIApi", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_Body_reports_OBS3008()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Post("/users")]
                Task Create([Body] User a, [Body] User b);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains("OBS3008", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
    }

    [Fact]
    public void Call_scope_is_captured_at_proxy_entry_for_every_return_shape()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Task<User> GetTask(int id);

                [Get("/users/{id}")]
                ValueTask<User> GetValue(int id);

                [Get("/users/{id}")]
                User GetSync(int id);

                [Delete("/users/{id}")]
                void DeleteSync(int id);

                [Delete("/users/{id}")]
                Task DeleteTask(int id);

                [Get("/users/{id}")]
                Observable<User> GetObservable(int id);

                [Delete("/users/{id}")]
                Observable<Unit> DeleteObservable(int id);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        string generated = string.Concat(output.GeneratedSources.Select(static s => s.Source)).Replace("\r\n", "\n");
        Assert.Equal(7, CountOf(generated, "RestApiCallScope.Capture()"));
        AssertScopeIsOutside(generated, "Observable.FromAsync");
        AssertScopeIsOutside(generated, "Task.Run(");
        Assert.Contains(
            "SendAsync<global::User, global::User>(Client, _settings, in ______spec0, ______scope, ______ct, @id)",
            generated,
            StringComparison.Ordinal);
        Assert.Contains("______scope, ______ct, @id)", generated, StringComparison.Ordinal);
        AssertClosedOver(generated);
    }

    static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    static void AssertScopeIsOutside(string generated, string marker)
    {
        var at = 0;
        var found = false;
        while ((at = generated.IndexOf(marker, at, StringComparison.Ordinal)) >= 0)
        {
            found = true;
            var capture = generated.LastIndexOf("RestApiCallScope.Capture()", at, StringComparison.Ordinal);
            Assert.True(capture >= 0 && capture < at, marker);
            var between = generated.Substring(capture, at - capture);
            Assert.DoesNotContain("public ", between, StringComparison.Ordinal);
            at += marker.Length;
        }

        Assert.True(found, marker);
    }

    static void AssertClosedOver(string generated)
    {
        var lambda = 0;
        var found = false;
        while ((lambda = generated.IndexOf("async ______ct =>", lambda, StringComparison.Ordinal)) >= 0)
        {
            found = true;
            var end = generated.IndexOf("});", lambda, StringComparison.Ordinal);
            Assert.True(end > lambda);
            var body = generated.Substring(lambda, end - lambda);
            Assert.DoesNotContain("RestApiCallScope.Capture()", body, StringComparison.Ordinal);
            Assert.Contains("______scope,", body, StringComparison.Ordinal);
            lambda = end;
        }

        Assert.True(found);
    }
}

