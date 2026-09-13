namespace Observables.Build.Tests;

static class RepoRoot
{
    public static string Find()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Observables.slnx")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate Observables.slnx from " + AppContext.BaseDirectory);
    }
}
