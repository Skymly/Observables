namespace Observables.Events.Tests;

public sealed class EventsPackagePropsTests
{
    [Theory]
    [InlineData("Observables.Events.R3.props")]
    [InlineData("Observables.Events.Reactive.props")]
    public void PackageId_props_register_ObservableRoutedEvents_and_UseWPF(string fileName)
    {
        string path = Path.Combine(FindRepoRoot(), "Observables.Events", "Observables.Events.Package", "build", fileName);
        Assert.True(File.Exists(path), path);

        string xml = File.ReadAllText(path);
        Assert.Contains("CompilerVisibleProperty", xml, StringComparison.Ordinal);
        Assert.Contains("Include=\"ObservableRoutedEvents\"", xml, StringComparison.Ordinal);
        Assert.Contains("Include=\"UseWPF\"", xml, StringComparison.Ordinal);
        Assert.Contains("<ObservableRoutedEvents", xml, StringComparison.Ordinal);
    }

    static string FindRepoRoot()
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
