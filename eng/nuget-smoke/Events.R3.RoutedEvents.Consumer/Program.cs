using System.Windows;
using System.Windows.Controls;

#if EVENTS_R3
using Observables.Events.R3;
using R3;
#else
using Observables.Events.Reactive;
#endif

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        Button button = new();
        VerifyRoutedEvent(
            button,
            () => new RoutedEventArgs(Button.ClickEvent, button),
            receive => button.RoutedEvents(handledEventsToo: true).Click.Subscribe(receive),
            receive => button.RoutedEventHandlers(handledEventsToo: true).Click.Subscribe(value => receive(value)));

        TypedButton typedButton = new();
        VerifyRoutedEvent(
            typedButton,
            () => new ChangedEventArgs(typedButton),
            receive => typedButton.RoutedEvents(handledEventsToo: true).ProbeChanged.Subscribe(receive),
            receive => typedButton.RoutedEventHandlers(handledEventsToo: true).ProbeChanged.Subscribe(value => receive(value)));

        Console.WriteLine("WPF routed events delivered the original payloads and stopped after disposal.");
    }

    static void VerifyRoutedEvent<TArgs>(
        UIElement control,
        Func<TArgs> createArgs,
        Func<Action<TArgs>, IDisposable> subscribeEvents,
        Func<Action<(object? sender, TArgs e)>, IDisposable> subscribeHandlers)
        where TArgs : RoutedEventArgs
    {
        TArgs raised = createArgs();
        raised.Handled = true;

        var events = new List<TArgs>();
        var handlers = new List<(object? sender, TArgs e)>();
        using (subscribeEvents(events.Add))
        using (subscribeHandlers(handlers.Add))
        {
            control.RaiseEvent(raised);
            if (events.Count != 1 || !ReferenceEquals(events[0], raised) || handlers.Count != 1
                || !ReferenceEquals(handlers[0].sender, control) || !ReferenceEquals(handlers[0].e, raised))
            {
                throw new InvalidOperationException("Expected one delivery of each WPF routed event shape.");
            }
        }

        control.RaiseEvent(createArgs());
        if (events.Count != 1 || handlers.Count != 1)
        {
            throw new InvalidOperationException("A disposed WPF routed subscription received another event.");
        }
    }
}

public delegate void ChangedEventHandler(object sender, ChangedEventArgs args);

public sealed class TypedButton : Button
{
    public static readonly RoutedEvent ProbeChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ProbeChanged), RoutingStrategy.Bubble, typeof(ChangedEventHandler), typeof(TypedButton));

    public event ChangedEventHandler ProbeChanged
    {
        add => AddHandler(ProbeChangedEvent, value);
        remove => RemoveHandler(ProbeChangedEvent, value);
    }
}

public sealed class ChangedEventArgs(TypedButton source) : RoutedEventArgs(TypedButton.ProbeChangedEvent, source)
{
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget) =>
        ((ChangedEventHandler)genericHandler)(genericTarget, this);
}
