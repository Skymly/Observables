using System.Linq;
using Observables.Events.Generators;
using Xunit;

namespace Observables.Events.Reactive.SourceGenerators.Tests;

public sealed class ObservableEventsReviewFollowUpTests
{
    [Fact]
    public void Unmanaged_constraint_does_not_also_emit_struct()
    {
        const string source = """
            namespace Demo;

            public class Foo<T> where T : unmanaged
            {
                public event System.Action? Changed;
            }

            public static class Usage
            {
                public static void Run(Foo<int> foo) => _ = foo.Events().Changed;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0449" or "CS8377");
        Assert.Contains("where T : unmanaged", snapshot);
        Assert.DoesNotContain("where T : struct, unmanaged", snapshot);
        Assert.Contains("Changed", snapshot);
    }

    [Fact]
    public void Single_interface_constraint_reuses_parent_events_interface()
    {
        const string source = """
            namespace Demo;

            public interface IFoo
            {
                event System.Action? Changed;
            }

            public static class Usage
            {
                public static void Run<T>(T source) where T : IFoo => _ = source.Events().Changed;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0101" or "CS0529");
        Assert.Contains("interface IFooEvents", snapshot);
        Assert.DoesNotContain("IFooEvents : IFooEvents", snapshot);
        Assert.Contains("Changed", snapshot);
    }

    [Fact]
    public void Distinct_generic_constraints_do_not_share_Events_TSource_signature()
    {
        const string source = """
            namespace Demo;

            public interface IFoo
            {
                event System.Action? FooChanged;
            }

            public interface IBar
            {
                event System.Action? BarChanged;
            }

            public static class Usage
            {
                public static void RunFoo<T>(T source) where T : IFoo => _ = source.Events().FooChanged;
                public static void RunBar<T>(T source) where T : IBar => _ = source.Events().BarChanged;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id == "CS0111");
        Assert.Contains("FooChanged", snapshot);
        Assert.Contains("BarChanged", snapshot);
    }

    [Fact]
    public void Hidden_event_with_incompatible_signature_reports_OBS2006()
    {
        const string source = """
            namespace Demo;

            public class BaseSource
            {
                public event System.Action? Changed;
            }

            public class DerivedSource : BaseSource
            {
                public new event System.Action<int>? Changed;
            }

            public static class Usage
            {
                public static void Run(DerivedSource source) => _ = source.Events().Changed;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0029" or "CS0738" or "CS0229");
        Assert.Contains("OBS2006", snapshot);
        Assert.Contains("Changed", snapshot);
    }

    [Fact]
    public void Keyword_event_name_is_escaped()
    {
        const string source = """
            namespace Demo;

            public class KeywordSource
            {
                public event System.Action? @event;
            }

            public static class Usage
            {
                public static void Run(KeywordSource source) => _ = source.Events().@event;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Contains("@event", snapshot);
    }

    [Fact]
    public void Ref_parameter_delegate_reports_OBS2001()
    {
        const string source = """
            namespace Demo;

            public delegate void RefHandler(ref int value);

            public class RefSource
            {
                public event RefHandler? Changed;
            }

            public static class Usage
            {
                public static void Run(RefSource source) => _ = source.Events();
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Contains("OBS2001", snapshot);
        Assert.DoesNotContain("IRefSourceEvents", snapshot);
    }

    [Fact]
    public void Nested_same_interface_names_are_disambiguated()
    {
        const string source = """
            public class A
            {
                public interface Foo
                {
                    event System.Action? FromA;
                }
            }

            public class B
            {
                public interface Foo
                {
                    event System.Action? FromB;
                }
            }

            public static class Usage
            {
                public static void Run(A.Foo a, B.Foo b)
                {
                    _ = a.Events().FromA;
                    _ = b.Events().FromB;
                }
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id == "CS0101");
        Assert.Contains("FromA", snapshot);
        Assert.Contains("FromB", snapshot);
    }

    [Fact]
    public void Sample_assembly_name_does_not_fake_Avalonia()
    {
        const string source = """
            namespace Demo;

            public class ClickSource
            {
                public event System.Action? Click;
            }

            public static class Usage
            {
                public static void Run(ClickSource source)
                {
                    _ = source.Events().Click;
                    _ = source.AttachedRoutedEvent();
                }
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain("global::Avalonia.", snapshot);
        Assert.DoesNotContain(
            output.GeneratedSources,
            static s => s.HintName.Contains("AttachedRoutedEvent", System.StringComparison.Ordinal));
        Assert.Contains("Click", snapshot);
    }

    const string WpfStubs = """
        namespace System.Windows
        {
            public class RoutedEvent {}
            public class RoutedEventArgs : System.EventArgs {}
            public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
        }
        namespace System.Windows.Controls
        {
            public class UIElement
            {
                public void AddHandler(System.Windows.RoutedEvent routedEvent, System.Delegate handler, bool handledEventsToo) {}
                public void RemoveHandler(System.Windows.RoutedEvent routedEvent, System.Delegate handler) {}
            }
            public class ActionButton : UIElement
            {
                public static readonly System.Windows.RoutedEvent ClickEvent = new();
                public event System.Action? Click;
            }
        }
        """;

    [Fact]
    public void Wpf_action_event_with_ClickEvent_field_does_not_use_EventHandler_shape()
    {
        const string source = WpfStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(System.Windows.Controls.ActionButton button)
                    {
                        _ = button.RoutedEvents().Click;
                    }
                }
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            useWpf: true,
            observableRoutedEvents: true);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id == "CS0738");
        Assert.DoesNotContain("EventHandler<global::System.Windows.RoutedEventArgs>", snapshot);
        Assert.Contains("Click", snapshot);
    }
}
