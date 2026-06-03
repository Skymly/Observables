using Observables.Events.Reactive;

ClickSource source = new();
using IDisposable sub = source.Events().Click.Subscribe();

Console.WriteLine("Observables.Events.Reactive consumer smoke OK");

public sealed class ClickSource
{
    public event Action? Click;
}
