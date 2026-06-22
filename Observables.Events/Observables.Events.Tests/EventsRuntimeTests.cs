using System.Collections.Generic;
using Observables.Events.R3;
using R3;

namespace Observables.Events.Tests;

public sealed class EventsRuntimeTests
{
    [Fact]
    public void Events_action_event_subscribes_and_triggers()
    {
        var source = new ClickSource();
        var clicks = new List<Unit>();
        using var sub = source.Events().Click.Subscribe(clicks.Add);

        source.RaiseClick();
        source.RaiseClick();

        Assert.Equal(2, clicks.Count);
    }

    [Fact]
    public void Events_action_with_argument_delivers_value()
    {
        var source = new ClickSource();
        var values = new List<int>();
        using var sub = source.Events().CountChanged.Subscribe(values.Add);

        source.RaiseCount(42);
        source.RaiseCount(7);

        Assert.Equal(new[] { 42, 7 }, values);
    }

    [Fact]
    public void EventHandlers_eventhandler_delivers_sender_and_args()
    {
        var source = new NotificationSource();
        var pairs = new List<(object? sender, System.EventArgs e)>();
        using var sub = source.EventHandlers().Notified.Subscribe(pairs.Add);

        var args = new System.EventArgs();
        source.RaiseNotified(args);

        Assert.Single(pairs);
        Assert.Same(source, pairs[0].sender);
        Assert.Same(args, pairs[0].e);
    }

    [Fact]
    public void EventHandlers_generic_eventhandler_delivers_typed_args()
    {
        var source = new NotificationSource();
        var pairs = new List<(object? sender, ValueChangedEventArgs e)>();
        using var sub = source.EventHandlers().ValueChanged.Subscribe(pairs.Add);

        var args = new ValueChangedEventArgs(99);
        source.RaiseValueChanged(args);

        Assert.Single(pairs);
        Assert.Same(source, pairs[0].sender);
        Assert.Equal(99, pairs[0].e.Value);
    }

    [Fact]
    public void Multiple_subscribers_share_same_event_stream()
    {
        var source = new ClickSource();
        var clicks1 = new List<Unit>();
        var clicks2 = new List<Unit>();
        using var sub1 = source.Events().Click.Subscribe(clicks1.Add);
        using var sub2 = source.Events().Click.Subscribe(clicks2.Add);

        source.RaiseClick();

        Assert.Single(clicks1);
        Assert.Single(clicks2);
    }

    [Fact]
    public void Unsubscribe_stops_receiving_events()
    {
        var source = new ClickSource();
        var clicks = new List<Unit>();
        var sub = source.Events().Click.Subscribe(clicks.Add);

        source.RaiseClick();
        sub.Dispose();
        source.RaiseClick();

        Assert.Single(clicks);
    }
}

public sealed class ClickSource
{
    public event Action? Click;
    public event Action<int>? CountChanged;

    public void RaiseClick() => Click?.Invoke();
    public void RaiseCount(int n) => CountChanged?.Invoke(n);
}

public sealed class NotificationSource
{
    public event System.EventHandler? Notified;
    public event System.EventHandler<ValueChangedEventArgs>? ValueChanged;

    public void RaiseNotified(System.EventArgs e) => Notified?.Invoke(this, e);
    public void RaiseValueChanged(ValueChangedEventArgs e) => ValueChanged?.Invoke(this, e);
}

public sealed class ValueChangedEventArgs(int value) : System.EventArgs
{
    public int Value { get; } = value;
}
