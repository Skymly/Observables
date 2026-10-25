using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Observables.Events.Generators;
using Observables.TestSupport;
using Xunit;

namespace Observables.Events.R3.SourceGenerators.Tests;

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

    // ── Visibility / accessibility (CS0122 regression shapes) ────────────

    const string CrossAssemblyLibrarySource = """
        namespace Framework;

        internal interface IInternalNotify
        {
            event System.Action? ImplicitlyImplemented;
            event System.Action? ExplicitlyImplemented;
        }

        public class Widget : IInternalNotify
        {
            public event System.Action? ImplicitlyImplemented;
            event System.Action? IInternalNotify.ExplicitlyImplemented { add { } remove { } }
        }
        """;

    static MetadataReference CompileCrossAssemblyLibrary()
    {
        var compilation = CSharpCompilation.Create(
            "Framework",
            [CSharpSyntaxTree.ParseText(CrossAssemblyLibrarySource, new CSharpParseOptions(LanguageVersion.Preview))],
            MetadataReferenceBuilder.Build(typeof(object)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics.Select(static d => d.ToString())));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    [Fact]
    public void Cross_assembly_internal_interface_events_emit_via_public_members()
    {
        const string source = """
            namespace Demo;

            public static class Usage
            {
                public static void Run(Framework.Widget w) => _ = w.Events();
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            extraReferences: [CompileCrossAssemblyLibrary()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        // Implicitly-implemented events surface through the public runtime member; the
        // explicitly-implemented one is unreachable and must not produce CS0122/CS0535.
        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0122" or "CS0535" or "CS0737" or "CS0738");
        Assert.Contains("ImplicitlyImplemented", snapshot);
        Assert.Contains("_sender.ImplicitlyImplemented", snapshot);
        Assert.DoesNotContain("ExplicitlyImplemented", snapshot);
    }

    [Fact]
    public void Explicitly_implemented_public_interface_event_uses_cast_accessor()
    {
        const string source = """
            namespace Demo;

            public interface IPublic
            {
                event System.Action? Changed;
            }

            public class Source : IPublic
            {
                event System.Action? IPublic.Changed { add { } remove { } }
            }

            public static class Usage
            {
                public static void Run(Source s) => _ = s.Events();
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("((global::Demo.IPublic)_sender).Changed", snapshot);
    }

    [Fact]
    public void Hidden_event_with_different_signature_uses_base_cast_accessor()
    {
        const string source = """
            namespace Demo;

            public class Base
            {
                public event System.Action? Changed;
            }

            public class Derived : Base
            {
                protected new event System.Action? Changed;
            }

            public static class Usage
            {
                public static void Run(Derived s) => _ = s.Events();
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        // _sender.Changed would bind the inaccessible protected member — cast to the declaring base.
        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS1540" or "CS0122");
        Assert.Contains("((global::Demo.Base)_sender).Changed", snapshot);
    }

    [Fact]
    public void Internal_event_on_same_assembly_source_is_wrapped()
    {
        const string source = """
            namespace Demo;

            public class Source
            {
                internal event System.Action? InternalChanged;
            }

            public static class Usage
            {
                public static void Run(Source s) => _ = s.Events().InternalChanged;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("InternalChanged", snapshot);
    }

    // ── Generic receivers (CS0246 regression shapes) ─────────────────────

    const string GenericWpfStubs = """
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
            public class GenericButton<T> : UIElement where T : class
            {
                public static readonly System.Windows.RoutedEvent ClickEvent = new();
                public event System.Windows.RoutedEventHandler? Click
                {
                    add => AddHandler(ClickEvent, value, false);
                    remove => RemoveHandler(ClickEvent, value);
                }
            }
        }
        """;

    [Fact]
    public void Generic_receiver_routed_extension_declares_receiver_type_parameters()
    {
        const string source = GenericWpfStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run<T>(System.Windows.Controls.GenericButton<T> button) where T : class
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

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0246" or "CS0308" or "CS0115");
        Assert.Contains("RoutedEvents<T>(this global::System.Windows.Controls.GenericButton<T> source", snapshot);
        Assert.Contains("where T : class", snapshot);
    }

    const string AttachedAvaloniaStubs = """
        namespace Avalonia.Interactivity
        {
            [System.Flags]
            public enum RoutingStrategies { Direct = 1, Bubble = 2, Tunnel = 4 }
            public class RoutedEventArgs : System.EventArgs {}
            public class RoutedEvent {}
            public class RoutedEvent<TEventArgs> : RoutedEvent where TEventArgs : RoutedEventArgs {}
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

                public void AddHandler(
                    Avalonia.Interactivity.RoutedEvent routedEvent,
                    System.Delegate handler,
                    Avalonia.Interactivity.RoutingStrategies routes,
                    bool handledEventsToo)
                {
                }

                public void RemoveHandler<TEventArgs>(
                    Avalonia.Interactivity.RoutedEvent<TEventArgs> routedEvent,
                    System.EventHandler<TEventArgs>? handler)
                    where TEventArgs : Avalonia.Interactivity.RoutedEventArgs
                {
                }

                public void RemoveHandler(Avalonia.Interactivity.RoutedEvent routedEvent, System.Delegate handler)
                {
                }
            }

            public class GenericPanel<T> : Control where T : class
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

            public class LegacyButton : Control
            {
                public static readonly Avalonia.Interactivity.RoutedEvent TapEvent = new();

                public event System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>? Tap
                {
                    add => AddHandler(TapEvent, value, Avalonia.Interactivity.RoutingStrategies.Bubble, false);
                    remove => RemoveHandler(TapEvent, value);
                }
            }
        }
        """;

    [Fact]
    public void Generic_receiver_attached_routed_event_declares_receiver_type_parameters()
    {
        const string source = AttachedAvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run<T>(Avalonia.Controls.GenericPanel<T> panel) where T : class
                    {
                        _ = panel.AttachedRoutedEvent(Avalonia.Controls.Button.ClickEvent);
                    }
                }
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0246" or "CS0311" or "CS0115");
        Assert.Contains("AttachedRoutedEvent<T, TEventArgs>(this global::Avalonia.Controls.GenericPanel<T> source", snapshot);
        Assert.Contains("where T : class", snapshot);
        Assert.Contains("where TEventArgs : global::Avalonia.Interactivity.RoutedEventArgs", snapshot);
    }

    // ── Avalonia non-generic RoutedEvent (CS1503 regression shape) ───────

    [Fact]
    public void Avalonia_non_generic_routed_event_field_uses_delegate_overload()
    {
        const string source = AttachedAvaloniaStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.LegacyButton button)
                    {
                        _ = button.RoutedEvents().Tap;
                    }
                }
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS1503" or "CS0411");
        Assert.Contains("TapEvent", snapshot);
        Assert.DoesNotContain("_sender.AddHandler<", snapshot);
        Assert.Contains("_sender.AddHandler(", snapshot);
    }

    [Fact]
    public void Avalonia_routed_event_with_non_routed_args_falls_back_to_clr()
    {
        const string source = AttachedAvaloniaStubs + """
            namespace Avalonia.Controls
            {
                public class WeirdButton : Control
                {
                    public static readonly Avalonia.Interactivity.RoutedEvent WeirdEvent = new();

                    public event System.EventHandler<System.EventArgs>? Weird;
                }
            }

            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(Avalonia.Controls.WeirdButton button)
                    {
                        _ = button.RoutedEvents().Weird;
                    }
                }
            }
            """;

        var output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()],
            observableRoutedEvents: true);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        // EventArgs does not derive from RoutedEventArgs — the AddHandler dispatch would fault,
        // so the CLR event subscription path (_sender.Weird +=) is used instead.
        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0311" or "CS1503");
        Assert.DoesNotContain("_sender.AddHandler", snapshot);
    }

    // ── Generic-constraint bootstrap merge & naming ──────────────────────

    [Fact]
    public void Same_generic_constraint_used_by_Events_and_EventHandlers_shares_bootstrap_class()
    {
        const string source = """
            namespace Demo;

            public interface IFoo
            {
                event System.EventHandler<System.EventArgs>? Changed;
            }

            public static class Usage
            {
                public static void Run<T>(T source) where T : IFoo
                {
                    _ = source.Events().Changed;
                    _ = source.EventHandlers().Changed;
                }
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0101" or "CS0111");
        Assert.Equal(
            2,
            output.GeneratedSources.Count(static s => s.Source.Contains("ObservableEventsBootstrapExtensions_global__Demo_IFoo", System.StringComparison.Ordinal)));
    }

    [Fact]
    public void Same_short_name_constraint_types_in_different_namespaces_get_qualified_names()
    {
        const string source = """
            namespace A
            {
                public interface IFoo
                {
                    event System.Action? FromA;
                }
            }

            namespace B
            {
                public interface IFoo
                {
                    event System.Action? FromB;
                }
            }

            public static class Usage
            {
                public static void RunA<T>(T source) where T : A.IFoo => _ = source.Events().FromA;
                public static void RunB<T>(T source) where T : B.IFoo => _ = source.Events().FromB;
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0101" or "CS0111");
        Assert.Contains("IA_FooEvents", snapshot);
        Assert.Contains("IB_FooEvents", snapshot);
    }

    // ── Incompatible interface merges (OBS2006) ──────────────────────────

    [Fact]
    public void Incompatible_same_name_events_across_interfaces_are_skipped()
    {
        const string source = """
            namespace Demo;

            public interface IA
            {
                event System.Action? Changed;
            }

            public interface IB
            {
                event System.EventHandler? Changed;
            }

            public interface IC : IA, IB
            {
                event System.Action<int>? Own;
            }

            public static class Usage
            {
                public static void Run(IC source) => _ = source.Events();
            }
            """;

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);
        var snapshot = GeneratorTestHarness.ToSnapshot(output);

        // OBS2006 fires for the conflicting name; IC keeps only its own event and drops the
        // base edges that would surface two incompatible "Changed" members.
        Assert.Contains(output.Diagnostics, static d => d.Id == "OBS2006");
        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0102" or "CS0535" or "CS0738");
        Assert.Contains("ICEvents", snapshot);
        Assert.DoesNotContain("ICEvents :", snapshot);
    }

    // ── Routed field accessibility ───────────────────────────────────────

    const string ProtectedFieldWpfStubs = """
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
            public class HiddenFieldButton : UIElement
            {
                protected static readonly System.Windows.RoutedEvent TapEvent = new();
                public event System.Windows.RoutedEventHandler? Tap
                {
                    add => AddHandler(TapEvent, value, false);
                    remove => RemoveHandler(TapEvent, value);
                }
            }
        }
        """;

    [Fact]
    public void Routed_field_without_accessibility_is_not_surfaced_as_routed()
    {
        const string source = ProtectedFieldWpfStubs + """
            namespace Demo
            {
                public static class Usage
                {
                    public static void Run(System.Windows.Controls.HiddenFieldButton button)
                    {
                        _ = button.RoutedEvents();
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

        // The protected TapEvent field cannot be named outside the type, so Tap is excluded from
        // the routed surface (the CLR event still flows through .Events()) — no AddHandler and
        // no generated reference to the inaccessible field.
        Assert.DoesNotContain(output.Diagnostics, static d => d.Id is "CS0122" or "CS1540");
        Assert.DoesNotContain("TapEvent", snapshot);
        Assert.DoesNotContain("_sender.AddHandler", snapshot);
    }

    // ── Static events entry removed ──────────────────────────────────────

    [Fact]
    public void Static_events_entry_produces_no_statics_sources()
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

        var output = GeneratorTestHarness.Run(source, generators: [new ObservableEventsGenerator()]);

        Assert.DoesNotContain(
            output.GeneratedSources,
            static s => s.Source.Contains("ObservableEventsStatics", System.StringComparison.Ordinal));
    }
}
