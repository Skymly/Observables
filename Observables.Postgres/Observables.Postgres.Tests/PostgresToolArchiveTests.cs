using System.Security.Cryptography;
using System.Text;
using Observables.Postgres.Tests.Infrastructure;

namespace Observables.Postgres.Tests;

public sealed class PostgresToolArchiveTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reuses_only_the_archive_with_the_expected_digest(bool cacheMatches)
    {
        var directory = Directory.CreateTempSubdirectory("observables-postgres-archive-");
        try
        {
            var path = Path.Combine(directory.FullName, "test-tools.jar");
            const string expected = "verified test contents";
            var content = Encoding.UTF8.GetBytes(expected);
            var sha256 = Convert.ToHexString(SHA256.HashData(content));
            var cancellationToken = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(path, cacheMatches ? expected : "stale test contents", cancellationToken);

            var downloads = 0;
            await using var archive = await PostgresTestServer.OpenPinnedArchiveAsync(path, sha256, _ =>
            {
                downloads++;
                return Task.FromResult<Stream>(new MemoryStream(content));
            }, cancellationToken);

            using var reader = new StreamReader(archive);
            Assert.Equal(expected, await reader.ReadToEndAsync(cancellationToken));
            Assert.Equal(expected, await File.ReadAllTextAsync(path, cancellationToken));
            Assert.Equal(cacheMatches ? 0 : 1, downloads);
            Assert.Equal(path, Assert.Single(Directory.GetFiles(directory.FullName)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_an_unverified_download_without_publishing_it()
    {
        var directory = Directory.CreateTempSubdirectory("observables-postgres-archive-");
        try
        {
            var path = Path.Combine(directory.FullName, "test-tools.jar");
            var sha256 = Convert.ToHexString(SHA256.HashData("expected test contents"u8));
            var cancellationToken = TestContext.Current.CancellationToken;

            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                PostgresTestServer.OpenPinnedArchiveAsync(
                    path,
                    sha256,
                    _ => Task.FromResult<Stream>(new MemoryStream("unexpected test contents"u8.ToArray())),
                    cancellationToken));

            Assert.Contains("SHA-256", error.Message, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
