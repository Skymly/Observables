using System.IO.Compression;
using System.Linq;

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

        Assert.Contains(
            ", false, _settings.Buffered, ______ct)",
            GeneratorTestHarness.ToSnapshot(output),
            StringComparison.Ordinal);
        return Task.CompletedTask;
    }

    [Fact]
    public Task GetUser_path_parameter_mismatch_reports_OBS3004()
    {
        GeneratorRunOutput output = GeneratorTestHarness.Run(
            """
            public interface IUserApi
            {
                [Get("/users/{id}")]
                Observable<User> GetUser(int id, int page);
            }

            public sealed class User
            {
                public int Id { get; set; }
            }
            """);

        Assert.Contains("OBS3004", GeneratorTestHarness.ToSnapshot(output), StringComparison.Ordinal);
        return Task.CompletedTask;
    }
}
