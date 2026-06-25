using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Observables.Events.Generators;
using VerifyXunit;
using Xunit;

namespace Observables.Events.Reactive.SourceGenerators.Tests;

public sealed class ObservableEventsGeneratorTests
{
    [Fact]
    public Task Generates_Events_wrapper_for_action_event()
    {
        const string source = """
            namespace Demo;

            public class ClickSource
            {
                public event System.Action? Click;
            }

            public static class Usage
            {
                public static void Run(ClickSource s) => s.Events();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Generates_Events_wrapper_for_interface_type()
    {
        const string source = """
            namespace Demo;

            public interface INotifySomething
            {
                event System.EventHandler<System.EventArgs>? SomethingChanged;
            }

            public interface INotifyMore : INotifySomething
            {
                event System.Action? MoreChanged;
            }

            public static class Usage
            {
                public static void Run(INotifyMore s)
                {
                    _ = s.Events().SomethingChanged;
                    _ = s.Events().MoreChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("SomethingChanged", snapshot);
        Assert.Contains("MoreChanged", snapshot);
    }

    [Fact]
    public void Generates_EventHandlers_wrapper_for_interface_type()
    {
        const string source = """
            namespace Demo;

            public interface INotifyPropertyChanged
            {
                event System.EventHandler? PropertyChanged;
            }

            public static class Usage
            {
                public static void Run(INotifyPropertyChanged s)
                {
                    _ = s.EventHandlers().PropertyChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("PropertyChanged", snapshot);
        Assert.Contains("System.Reactive.Linq.Observable.FromEvent", snapshot);
    }

    [Fact]
    public void Generates_Events_wrapper_for_generic_class()
    {
        const string source = """
            namespace Demo;

            public class GenericSource<T>
            {
                public event System.Action<T>? ValueChanged;
            }

            public static class Usage
            {
                public static void Run(GenericSource<string> s)
                {
                    _ = s.Events().ValueChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("ValueChanged", snapshot);
    }

    [Fact]
    public void Generates_Events_wrapper_for_generic_constraints()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.Action? BaseChanged;
            }

            public interface IFirst
            {
                event System.EventHandler<System.EventArgs>? FirstChanged;
            }

            public interface ISecond
            {
                event System.Action<int>? SecondChanged;
            }

            public static class Usage
            {
                public static void Run<TSource>(TSource source)
                    where TSource : BaseSource, IFirst, ISecond
                {
                    _ = source.Events().BaseChanged;
                    _ = source.Events().FirstChanged;
                    _ = source.Events().SecondChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("where TSource : global::Demo.BaseSource, global::Demo.IFirst, global::Demo.ISecond", snapshot);
        Assert.Contains("((global::Demo.BaseSource)_sender).BaseChanged", snapshot);
        Assert.Contains("((global::Demo.IFirst)_sender).FirstChanged", snapshot);
        Assert.Contains("((global::Demo.ISecond)_sender).SecondChanged", snapshot);
        Assert.Contains("IBaseSource_First_SecondEvents", snapshot);
        Assert.Contains("IBaseSourceEvents", snapshot);
        Assert.Contains("IFirstEvents", snapshot);
        Assert.Contains("ISecondEvents", snapshot);
    }

    [Fact]
    public void Generates_EventHandlers_wrapper_for_generic_constraints()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.EventHandler<System.EventArgs>? BaseChanged;
            }

            public interface IFirst
            {
                event System.EventHandler<System.EventArgs>? FirstChanged;
            }

            public static class Usage
            {
                public static void Run<TSource>(TSource source)
                    where TSource : BaseSource, IFirst
                {
                    _ = source.EventHandlers().BaseChanged;
                    _ = source.EventHandlers().FirstChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("where TSource : global::Demo.BaseSource, global::Demo.IFirst", snapshot);
        Assert.Contains("global::System.Reactive.Linq.Observable.FromEvent", snapshot);
        Assert.Contains("((global::Demo.BaseSource)_sender).BaseChanged", snapshot);
        Assert.Contains("((global::Demo.IFirst)_sender).FirstChanged", snapshot);
        Assert.Contains("IBaseSource_FirstEventHandlers", snapshot);
    }

    [Fact]
    public void Generates_interface_hierarchy_for_derived_class()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.Action? BaseChanged;
            }

            public interface INotify
            {
                event System.EventHandler<System.EventArgs>? Notified;
            }

            public class DerivedSource : BaseSource, INotify
            {
                public event System.Action<int>? DerivedChanged;
                public event System.EventHandler<System.EventArgs>? Notified;
            }

            public static class Usage
            {
                public static void Run(DerivedSource s)
                {
                    _ = s.Events().BaseChanged;
                    _ = s.Events().DerivedChanged;
                    _ = s.Events().Notified;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));

        Assert.Contains("interface IBaseSourceEvents", snapshot);
        Assert.Contains("interface INotifyEvents", snapshot);
        Assert.Contains("interface IDerivedSourceEvents : IBaseSourceEvents", snapshot);
        Assert.Contains("class DerivedSourceEventsImpl : IDerivedSourceEvents", snapshot);
        Assert.Contains("IDerivedSourceEvents Events(this global::Demo.DerivedSource source)", snapshot);

        Assert.Contains("DerivedChanged", snapshot);
        Assert.Contains("BaseChanged", snapshot);
        Assert.Contains("Notified", snapshot);
    }

    [Fact]
    public void Reports_diagnostic_for_unsupported_event_delegate()
    {
        const string source = """
            namespace Demo;

            public class MixedSource
            {
                public event System.Action? Good;
                public event System.Func<int>? Bad;
            }

            public static class Usage
            {
                public static void Run(MixedSource s) => s.Events();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("OBS2001", snapshot);
        Assert.Contains("Good", snapshot);
    }

    [Fact]
    public void Reports_diagnostic_for_unsupported_from_event_handlers_delegate()
    {
        const string source = """
            namespace Demo;

            public class MixedHandlerSource
            {
                public event System.EventHandler? Good;
                public event System.Action<int>? Bad;
            }

            public static class Usage
            {
                public static void Run(MixedHandlerSource s) => s.EventHandlers();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("OBS2002", snapshot);
        Assert.Contains("Good", snapshot);
    }

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
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);

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
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(
            output.GeneratedSources,
            static s => s.HintName.Contains("AttachedRoutedEvent.g.cs", StringComparison.Ordinal));
        Assert.Contains(
            output.GeneratedSources,
            static s => s.HintName.Contains("AttachedRoutedEventHandler.g.cs", StringComparison.Ordinal));

        string snapshot = GeneratorTestHarness.ToSnapshot(output);
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
            useWpf: false,
            observableRoutedEvents: true);

        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain("IButtonRoutedEvents", snapshot);
    }

    [Fact]
    public void Does_not_emit_routed_events_when_ObservableRoutedEvents_false()
    {
        const string source = """
            namespace Demo;

            public class ClickSource
            {
                public event System.Action? Click;
            }

            public static class Usage
            {
                public static void Run(ClickSource s) => s.Events();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: false);

        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.DoesNotContain("IButtonRoutedEvents", snapshot);
        Assert.DoesNotContain(".RoutedEvents.g.cs", snapshot);
        Assert.Contains("IClickSourceEvents", snapshot);
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

