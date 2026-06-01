using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Observables.Events.Reactive.SourceGenerators;
using VerifyXunit;
using Xunit;

namespace Observables.Events.Reactive.SourceGenerators.Tests;

public sealed class ObservableEventsGeneratorTests
{
    [Fact]
    public Task Generates_FromEvents_wrapper_for_action_event()
    {
        const string source = """
            namespace Demo;

            public class ClickSource
            {
                public event System.Action? Click;
            }

            public static class Usage
            {
                public static void Run(ClickSource s) => s.FromEvents();
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);

        return Verifier.Verify(GeneratorTestHarness.ToSnapshot(output));
    }

    [Fact]
    public void Generates_FromEvents_wrapper_for_interface_type()
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
                    _ = s.FromEvents().SomethingChanged;
                    _ = s.FromEvents().MoreChanged;
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
    public void Generates_FromEventHandlers_wrapper_for_interface_type()
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
                    _ = s.FromEventHandlers().PropertyChanged;
                }
            }
            """;

        GeneratorRunOutput output = GeneratorTestHarness.Run(
            source,
            generators: [new ObservableEventsGenerator()]);
        string snapshot = GeneratorTestHarness.ToSnapshot(output);

        Assert.Empty(output.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("PropertyChanged", snapshot);
        Assert.Contains("FromEventHandler", snapshot);
    }

    [Fact]
    public void Generates_FromEvents_wrapper_for_generic_class()
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
                    _ = s.FromEvents().ValueChanged;
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
    public void Generates_FromEvents_wrapper_for_generic_constraints()
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
                    _ = source.FromEvents().BaseChanged;
                    _ = source.FromEvents().FirstChanged;
                    _ = source.FromEvents().SecondChanged;
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
    public void Generates_FromEventHandlers_wrapper_for_generic_constraints()
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
                    _ = source.FromEventHandlers().BaseChanged;
                    _ = source.FromEventHandlers().FirstChanged;
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
                    _ = s.FromEvents().BaseChanged;
                    _ = s.FromEvents().DerivedChanged;
                    _ = s.FromEvents().Notified;
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
        Assert.Contains("IDerivedSourceEvents FromEvents(this global::Demo.DerivedSource source)", snapshot);

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
                public static void Run(MixedSource s) => s.FromEvents();
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
                public static void Run(MixedHandlerSource s) => s.FromEventHandlers();
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
}
