using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Observables.RoutedEvents.R3.SourceGenerators;
using VerifyXunit;
using Xunit;

namespace Observables.RoutedEvents.R3.SourceGenerators.Tests;

public sealed class RoutedEventsGeneratorTests
{
    [Fact]
    public Task Generates_Avalonia_routed_event_wrappers()
    {
        const string source = AvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.Button button)
                    {
                        _ = button.RoutedEvents().Click;
                        _ = button.RoutedEventHandlers(Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true).Click;
                    }
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Generates_Avalonia_attached_routed_event_extensions()
    {
        const string source = AvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.Panel panel)
                    {
                        _ = panel.AttachedRoutedEvent(Avalonia.Controls.Button.ClickEvent, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
                        _ = panel.AttachedRoutedEventHandler(Avalonia.Controls.Button.ClickEvent);
                    }
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);

        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("AttachedRoutedEvent<TEventArgs>", snapshot);
        Assert.Contains("AttachedRoutedEventHandler<TEventArgs>", snapshot);
        Assert.Contains("source.AddHandler", snapshot);
        Assert.Contains("routedEvent", snapshot);
        Assert.Contains("handledEventsToo", snapshot);
    }

    [Fact]
    public void Does_not_emit_Wpf_routed_events_without_UseWPF()
    {
        const string source = WpfStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(System.Windows.Controls.Button button) => button.RoutedEvents();
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            useWpf: false);

        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain("IButtonRoutedEvents", snapshot);
    }

    private const string AvaloniaStubs = """
        namespace Avalonia.Interactivity
        {
            [System.Flags]
            public enum RoutingStrategies
            {
                Direct = 1,
                Bubble = 2,
                Tunnel = 4,
            }

            public class RoutedEventArgs : System.EventArgs
            {
            }

            public class RoutedEvent<TEventArgs>
                where TEventArgs : RoutedEventArgs
            {
            }
        }

        namespace Avalonia.Controls
        {
            public class Control
            {
                public void AddHandler<TEventArgs>(
                    Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    System.EventHandler<TEventArgs>? handler,
                    Avalonia.Interactivity.RoutingStrategies routes = Avalonia.Interactivity.RoutingStrategies.Direct | Avalonia.Interactivity.RoutingStrategies.Bubble,
                    bool handledEventsToo = false)
                    where TEventArgs : Avalonia.Interactivity.RoutedEventArgs
                {
                }

                public void RemoveHandler<TEventArgs>(
                    Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    System.EventHandler<TEventArgs>? handler)
                    where TEventArgs : Avalonia.Interactivity.RoutedEventArgs
                {
                }
            }

            public class Panel : Control
            {
            }

            public class Button : Control
            {
                public static readonly Avalonia.Interactivity.RoutedEvent<Avalonia.Interactivity.RoutedEventArgs> ClickEvent = new();

                public event System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>? Click
                {
                    add => AddHandler(ClickEvent, value);
                    remove => RemoveHandler(ClickEvent, value);
                }
            }
        }

        """;

    private const string WpfStubs = """
        namespace System.Windows
        {
            public class RoutedEvent
            {
            }

            public class RoutedEventArgs : System.EventArgs
            {
            }
        }

        namespace System.Windows.Controls
        {
            public class UIElement
            {
                public void AddHandler(System.Windows.RoutedEvent routedEvent, System.Delegate handler, bool handledEventsToo)
                {
                }

                public void RemoveHandler(System.Windows.RoutedEvent routedEvent, System.Delegate handler)
                {
                }
            }

            public class Button : UIElement
            {
                public static readonly System.Windows.RoutedEvent ClickEvent = new();

                public event System.Windows.RoutedEventHandler? Click
                {
                    add => AddHandler(ClickEvent, value, false);
                    remove => RemoveHandler(ClickEvent, value);
                }
            }
        }

        namespace System.Windows
        {
            public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
        }

        """;
}
