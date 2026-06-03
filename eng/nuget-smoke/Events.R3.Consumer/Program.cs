using Observables.Events.R3;
using R3;

ClickSource source = new();
using IDisposable sub = source.Events().Click.Subscribe(_ => { });

Console.WriteLine("Observables.Events.R3 consumer smoke OK");

public sealed class ClickSource
{
    public event Action? Click;
}
