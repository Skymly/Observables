using System.Windows.Controls;
using Observables.Events.R3;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        Button button = new();
        var events = button.RoutedEvents();
        var stream = events.Click;
        if (stream is null)
        {
            throw new InvalidOperationException("Events R3 routed generated wrapper was not created.");
        }

        Console.WriteLine("Observables.Events.R3 routed-events consumer smoke OK");
    }
}
