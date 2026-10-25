using System.Collections.Immutable;

namespace Observables.Roslyn.Shared;

/// <summary>
/// Pure proxy-domain catalog shared by Analyzers and CodeFixes (linked into both assemblies).
/// Per-domain identity lives here: interface marker, diagnostic ids (empty interface OBS*007,
/// missing boundary OBS*001, shape mismatch OBS*004, missing runtime package OBS*002, missing
/// reactive package OBS*005), boundary attribute suggestions, package ids, and assembly names.
/// No <c>DiagnosticDescriptor</c> — analyzers bind empty-interface descriptors separately.
/// </summary>
internal static class ProxyDomainTable
{
    internal enum DomainKind
    {
        SignalR,
        Mqtt,
        WebSocket,
        Grpc,
        Sse,
        Nats,
        Postgres,
        Redis,
        RestApi,
        Events,
    }

    /// <summary>
    /// Observable backends per domain. Packages for the same domain are pairwise exclusive
    /// (OBS0001). <see cref="BackendKind.Primitives"/> is modeled ahead of the package itself
    /// (ADR-005).
    /// </summary>
    internal enum BackendKind
    {
        R3,
        Reactive,
        Primitives,
    }

    internal static readonly IReadOnlyList<BackendKind> BackendKinds =
        [BackendKind.R3, BackendKind.Reactive, BackendKind.Primitives];

    internal sealed class BoundaryAttributeSuggestion
    {
        internal BoundaryAttributeSuggestion(string displayText, string insertText)
        {
            DisplayText = displayText;
            InsertText = insertText;
        }

        internal string DisplayText { get; }
        internal string InsertText { get; }

        internal string AttributeTypeName => InsertText.EndsWith("Attribute", StringComparison.Ordinal)
            ? InsertText
            : InsertText + "Attribute";
    }

    internal sealed class ProxyDomainDefinition
    {
        internal ProxyDomainDefinition(
            DomainKind kind,
            string displayName,
            string interfaceMarkerMetadataName,
            string reactiveAdapterMetadataName,
            string emptyInterfaceDiagnosticId,
            string missingBoundaryDiagnosticId,
            string memberShapeMismatchDiagnosticId,
            string missingRuntimePackageDiagnosticId,
            string missingReactivePackageDiagnosticId,
            IReadOnlyList<BoundaryAttributeSuggestion> methodAttributes,
            IReadOnlyList<BoundaryAttributeSuggestion> propertyAttributes,
            string? r3MarkerMetadataName = null,
            string? reactiveMarkerMetadataName = null,
            string? primitivesMarkerMetadataName = null)
        {
            Kind = kind;
            DisplayName = displayName;
            InterfaceMarkerMetadataName = interfaceMarkerMetadataName;
            ReactiveAdapterMetadataName = reactiveAdapterMetadataName;
            EmptyInterfaceDiagnosticId = emptyInterfaceDiagnosticId;
            MissingBoundaryDiagnosticId = missingBoundaryDiagnosticId;
            MemberShapeMismatchDiagnosticId = memberShapeMismatchDiagnosticId;
            MissingRuntimePackageDiagnosticId = missingRuntimePackageDiagnosticId;
            MissingReactivePackageDiagnosticId = missingReactivePackageDiagnosticId;
            MethodAttributes = methodAttributes;
            PropertyAttributes = propertyAttributes;
            R3MarkerMetadataName = r3MarkerMetadataName;
            ReactiveMarkerMetadataName = reactiveMarkerMetadataName;
            PrimitivesMarkerMetadataName = primitivesMarkerMetadataName;
        }

        internal DomainKind Kind { get; }
        internal string DisplayName { get; }
        internal string InterfaceMarkerMetadataName { get; }
        internal string ReactiveAdapterMetadataName { get; }

        /// <summary>Empty-interface diagnostic (OBS*007) for this domain's empty marked interfaces.</summary>
        internal string EmptyInterfaceDiagnosticId { get; }

        /// <summary>Missing-boundary-attribute diagnostic (OBS*001; HTTP verbs OBS3001 for RestAPI).</summary>
        internal string MissingBoundaryDiagnosticId { get; }

        /// <summary>Member-shape-mismatch diagnostic (OBS*004; path templates OBS3004 for RestAPI).</summary>
        internal string MemberShapeMismatchDiagnosticId { get; }

        /// <summary>Missing-runtime-package diagnostic (OBS*002), fixed by adding <see cref="RuntimePackageName"/>.</summary>
        internal string MissingRuntimePackageDiagnosticId { get; }

        /// <summary>Missing-reactive-package diagnostic (OBS*005), fixed by adding <see cref="ReactivePackageName"/>.</summary>
        internal string MissingReactivePackageDiagnosticId { get; }

        internal IReadOnlyList<BoundaryAttributeSuggestion> MethodAttributes { get; }
        internal IReadOnlyList<BoundaryAttributeSuggestion> PropertyAttributes { get; }

        /// <summary>Runtime package id, following the Observables.&lt;Feature&gt; naming convention.</summary>
        internal string RuntimePackageName => $"Observables.{DisplayName}";

        /// <summary>R3 bridge assembly name; equals <see cref="R3PackageName"/> by repository convention.</summary>
        internal string R3AssemblyName => $"Observables.{DisplayName}.R3";

        /// <summary>Reactive bridge assembly name; equals <see cref="ReactivePackageName"/> by repository convention.</summary>
        internal string ReactiveAssemblyName => $"Observables.{DisplayName}.Reactive";

        /// <summary>Primitives bridge assembly name; equals <see cref="PrimitivesPackageName"/> by repository convention.</summary>
        internal string PrimitivesAssemblyName => $"Observables.{DisplayName}.Primitives";

        internal string R3PackageName => R3AssemblyName;

        internal string ReactivePackageName => ReactiveAssemblyName;

        /// <summary>ReactiveUI.Primitives backend package id (ADR-005; package not yet published).</summary>
        internal string PrimitivesPackageName => PrimitivesAssemblyName;

        /// <summary>
        /// Metadata name of a type the backend's generator emits into the consuming compilation
        /// (or a type in the backend bridge assembly). Used by OBS0001's dev-time symbol fallback
        /// when the package-declared <c>build_property</c> signal is absent.
        /// </summary>
        internal string? R3MarkerMetadataName { get; }

        /// <inheritdoc cref="R3MarkerMetadataName"/>
        internal string? ReactiveMarkerMetadataName { get; }

        /// <inheritdoc cref="R3MarkerMetadataName"/>
        internal string? PrimitivesMarkerMetadataName { get; }

        internal string BackendPackageName(BackendKind backend) => backend switch
        {
            BackendKind.R3 => R3PackageName,
            BackendKind.Reactive => ReactivePackageName,
            BackendKind.Primitives => PrimitivesPackageName,
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };

        internal string BackendAssemblyName(BackendKind backend) => backend switch
        {
            BackendKind.R3 => R3AssemblyName,
            BackendKind.Reactive => ReactiveAssemblyName,
            BackendKind.Primitives => PrimitivesAssemblyName,
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };

        /// <summary>
        /// MSBuild property the backend package's buildTransitive props set to <c>true</c>
        /// (consumed by OBS0001 as <c>build_property.&lt;key&gt;</c>).
        /// </summary>
        internal string BackendKeyName(BackendKind backend) =>
            $"Observables{DisplayName}Backend_{backend}";

        internal string? BackendMarkerMetadataName(BackendKind backend) => backend switch
        {
            BackendKind.R3 => R3MarkerMetadataName,
            BackendKind.Reactive => ReactiveMarkerMetadataName,
            BackendKind.Primitives => PrimitivesMarkerMetadataName,
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };

        internal IEnumerable<string> BackendPackageIds =>
            BackendKinds.Select(BackendPackageName);

        internal string? DefaultMethodAttribute(string memberName) =>
            FormatAttribute(MethodAttributes.Count > 0 ? MethodAttributes[0] : null, memberName);

        internal string? DefaultPropertyAttribute(string memberName) =>
            FormatAttribute(PropertyAttributes.Count > 0 ? PropertyAttributes[0] : null, memberName);

        static string? FormatAttribute(BoundaryAttributeSuggestion? suggestion, string memberName)
        {
            if (suggestion is null)
            {
                return null;
            }

            if (suggestion.InsertText.IndexOf('(') >= 0)
            {
                return suggestion.InsertText.StartsWith("[", StringComparison.Ordinal)
                    ? suggestion.InsertText
                    : $"[{suggestion.InsertText}";
            }

            return $"[{suggestion.InsertText}(\"{memberName}\")]";
        }
    }

    internal static readonly ProxyDomainDefinition SignalR = new(
        DomainKind.SignalR,
        displayName: "SignalR",
        interfaceMarkerMetadataName: "Observables.SignalR.HubAttribute",
        reactiveAdapterMetadataName: "Observables.SignalR.Reactive.SystemReactiveSignalRAdapter",
        emptyInterfaceDiagnosticId: "OBS4007",
        missingBoundaryDiagnosticId: "OBS4001",
        memberShapeMismatchDiagnosticId: "OBS4004",
        missingRuntimePackageDiagnosticId: "OBS4002",
        missingReactivePackageDiagnosticId: "OBS4005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("HubInvoke", "HubInvoke"),
            new BoundaryAttributeSuggestion("HubSend", "HubSend"),
            new BoundaryAttributeSuggestion("HubStream", "HubStream"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("HubOn", "HubOn"),
        ]);

    internal static readonly ProxyDomainDefinition Mqtt = new(
        DomainKind.Mqtt,
        displayName: "Mqtt",
        interfaceMarkerMetadataName: "Observables.Mqtt.MqttAttribute",
        reactiveAdapterMetadataName: "Observables.Mqtt.Reactive.SystemReactiveMqttAdapter",
        emptyInterfaceDiagnosticId: "OBS5007",
        missingBoundaryDiagnosticId: "OBS5001",
        memberShapeMismatchDiagnosticId: "OBS5004",
        missingRuntimePackageDiagnosticId: "OBS5002",
        missingReactivePackageDiagnosticId: "OBS5005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("MqttPublish", "MqttPublish"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("MqttSubscribe", "MqttSubscribe"),
        ]);

    internal static readonly ProxyDomainDefinition WebSocket = new(
        DomainKind.WebSocket,
        displayName: "WebSocket",
        interfaceMarkerMetadataName: "Observables.WebSocket.WebSocketAttribute",
        reactiveAdapterMetadataName: "Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter",
        emptyInterfaceDiagnosticId: "OBS6007",
        missingBoundaryDiagnosticId: "OBS6001",
        memberShapeMismatchDiagnosticId: "OBS6004",
        missingRuntimePackageDiagnosticId: "OBS6002",
        missingReactivePackageDiagnosticId: "OBS6005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("WebSocketSend", "WebSocketSend"),
            new BoundaryAttributeSuggestion("WebSocketConnect", "WebSocketConnect"),
            new BoundaryAttributeSuggestion("WebSocketClose", "WebSocketClose"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("WebSocketReceive", "WebSocketReceive"),
        ]);

    internal static readonly ProxyDomainDefinition Grpc = new(
        DomainKind.Grpc,
        displayName: "Grpc",
        interfaceMarkerMetadataName: "Observables.Grpc.GrpcAttribute",
        reactiveAdapterMetadataName: "Observables.Grpc.Reactive.SystemReactiveGrpcAdapter",
        emptyInterfaceDiagnosticId: "OBS7007",
        missingBoundaryDiagnosticId: "OBS7001",
        memberShapeMismatchDiagnosticId: "OBS7004",
        missingRuntimePackageDiagnosticId: "OBS7002",
        missingReactivePackageDiagnosticId: "OBS7005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("GrpcUnary", "GrpcUnary"),
            new BoundaryAttributeSuggestion("GrpcServerStream", "GrpcServerStream"),
            new BoundaryAttributeSuggestion("GrpcClientStream", "GrpcClientStream"),
            new BoundaryAttributeSuggestion("GrpcDuplex", "GrpcDuplex"),
        ],
        propertyAttributes: []);

    internal static readonly ProxyDomainDefinition Sse = new(
        DomainKind.Sse,
        displayName: "Sse",
        interfaceMarkerMetadataName: "Observables.Sse.SseAttribute",
        reactiveAdapterMetadataName: "Observables.Sse.Reactive.SystemReactiveSseAdapter",
        emptyInterfaceDiagnosticId: "OBS8007",
        missingBoundaryDiagnosticId: "OBS8001",
        memberShapeMismatchDiagnosticId: "OBS8004",
        missingRuntimePackageDiagnosticId: "OBS8002",
        missingReactivePackageDiagnosticId: "OBS8005",
        methodAttributes: [],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("SseEvent", "SseEvent"),
        ]);

    internal static readonly ProxyDomainDefinition Nats = new(
        DomainKind.Nats,
        displayName: "Nats",
        interfaceMarkerMetadataName: "Observables.Nats.NatsAttribute",
        reactiveAdapterMetadataName: "Observables.Nats.Reactive.SystemReactiveNatsAdapter",
        emptyInterfaceDiagnosticId: "OBS9007",
        missingBoundaryDiagnosticId: "OBS9001",
        memberShapeMismatchDiagnosticId: "OBS9004",
        missingRuntimePackageDiagnosticId: "OBS9002",
        missingReactivePackageDiagnosticId: "OBS9005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("NatsPublish", "NatsPublish"),
            new BoundaryAttributeSuggestion("NatsRequest", "NatsRequest"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("NatsSubscribe", "NatsSubscribe"),
        ]);

    internal static readonly ProxyDomainDefinition Postgres = new(
        DomainKind.Postgres,
        displayName: "Postgres",
        interfaceMarkerMetadataName: "Observables.Postgres.PostgresAttribute",
        reactiveAdapterMetadataName: "Observables.Postgres.Reactive.SystemReactivePostgresAdapter",
        emptyInterfaceDiagnosticId: "OBS10007",
        missingBoundaryDiagnosticId: "OBS10001",
        memberShapeMismatchDiagnosticId: "OBS10004",
        missingRuntimePackageDiagnosticId: "OBS10002",
        missingReactivePackageDiagnosticId: "OBS10005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("Notify", "Notify"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("Listen", "Listen"),
        ]);

    internal static readonly ProxyDomainDefinition Redis = new(
        DomainKind.Redis,
        displayName: "Redis",
        interfaceMarkerMetadataName: "Observables.Redis.RedisAttribute",
        reactiveAdapterMetadataName: "Observables.Redis.Reactive.SystemReactiveRedisAdapter",
        emptyInterfaceDiagnosticId: "OBS11007",
        missingBoundaryDiagnosticId: "OBS11001",
        memberShapeMismatchDiagnosticId: "OBS11004",
        missingRuntimePackageDiagnosticId: "OBS11002",
        missingReactivePackageDiagnosticId: "OBS11005",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("RedisPublish", "RedisPublish"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("RedisSubscribe", "RedisSubscribe"),
        ]);

    // RestAPI boundary attributes are the HTTP verb attributes ([Get], [Post], ...) and its
    // shape check is path-template/parameter sync, so OBS3001/OBS3004 are owned by dedicated
    // RestAPI code fixes. RestAPI is therefore excluded from MemberBoundaryDomains: the generic
    // add-boundary-attribute and method/property-shape fixes cannot pick a verb or sync a path.
    internal static readonly ProxyDomainDefinition RestApi = new(
        DomainKind.RestApi,
        displayName: "RestAPI",
        interfaceMarkerMetadataName: "Observables.RestAPI.RestApiAttribute",
        reactiveAdapterMetadataName: "Observables.RestAPI.Reactive.SystemReactiveObservableAdapter",
        emptyInterfaceDiagnosticId: "OBS3007",
        missingBoundaryDiagnosticId: "OBS3001",
        memberShapeMismatchDiagnosticId: "OBS3004",
        missingRuntimePackageDiagnosticId: "OBS3002",
        missingReactivePackageDiagnosticId: "OBS3005",
        methodAttributes: [],
        propertyAttributes: [],
        // RestAPI emits no bridge assembly for R3 and generates into
        // Observables.RestAPI.Implementation for both backends; the dedicated marker types are
        // the only reliable symbol-level backend signal.
        r3MarkerMetadataName: "Observables.RestAPI.Generated.R3BackendMarker",
        reactiveMarkerMetadataName: "Observables.RestAPI.Reactive.Generated.ReactiveBackendMarker");

    /// <summary>
    /// Events is generator-only (no interface-proxy marker). It still participates in OBS0001
    /// consumer-package conflict detection as Observables.Events.R3 vs Observables.Events.Reactive.
    /// </summary>
    internal static readonly ProxyDomainDefinition Events = new(
        DomainKind.Events,
        displayName: "Events",
        interfaceMarkerMetadataName: "",
        reactiveAdapterMetadataName: "",
        emptyInterfaceDiagnosticId: "",
        missingBoundaryDiagnosticId: "",
        memberShapeMismatchDiagnosticId: "",
        missingRuntimePackageDiagnosticId: "",
        missingReactivePackageDiagnosticId: "",
        methodAttributes: [],
        propertyAttributes: [],
        // Events ships no bridge assemblies; each backend generator emits a bootstrap type in a
        // backend-specific namespace instead.
        r3MarkerMetadataName: "Observables.Events.R3.ObservableEventsBootstrapExtensions",
        reactiveMarkerMetadataName: "Observables.Events.Reactive.ObservableEventsBootstrapExtensions");

    /// <summary>
    /// Domains with interface markers (including RestAPI). Used by empty-interface analysis.
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> InterfaceProxyDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

    /// <summary>
    /// Consumer packages that participate in OBS0001 R3 vs Reactive conflict detection.
    /// Separate from <see cref="InterfaceProxyDomains"/> so Events can be listed without
    /// becoming an empty-interface / member-boundary domain.
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> ConsumerPackageConflictDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi, Events];

    /// <summary>
    /// Domains that participate in the generic member missing-attribute / shape CodeFixes.
    /// RestAPI is excluded: its OBS3001/OBS3004 diagnostics are HTTP-specific (verb attributes,
    /// path templates) and are fixed by dedicated RestAPI code fixes.
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> MemberBoundaryDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis];

    internal static readonly string[] RestApiHttpMethodNames =
        ["Get", "Post", "Put", "Delete", "Patch", "Head", "Options"];

    internal static readonly ImmutableArray<string> MissingBoundaryDiagnosticIds =
        MemberBoundaryDomains
            .Select(d => d.MissingBoundaryDiagnosticId)
            .ToImmutableArray();

    internal static readonly ImmutableArray<string> MemberShapeMismatchDiagnosticIds =
        MemberBoundaryDomains
            .Select(d => d.MemberShapeMismatchDiagnosticId)
            .ToImmutableArray();

    internal static readonly ImmutableHashSet<string> MethodAttributeTypeNames =
        MemberBoundaryDomains
            .SelectMany(d => d.MethodAttributes)
            .Select(a => a.AttributeTypeName)
            .ToImmutableHashSet(StringComparer.Ordinal);

    internal static readonly ImmutableHashSet<string> PropertyAttributeTypeNames =
        MemberBoundaryDomains
            .SelectMany(d => d.PropertyAttributes)
            .Select(a => a.AttributeTypeName)
            .ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>Missing-runtime-package diagnostic (OBS*002) to runtime package id.</summary>
    internal static readonly ImmutableDictionary<string, string> RuntimePackageByDiagnosticId =
        InterfaceProxyDomains.ToImmutableDictionary(
            d => d.MissingRuntimePackageDiagnosticId,
            d => d.RuntimePackageName,
            StringComparer.Ordinal);

    /// <summary>Missing-reactive-package diagnostic (OBS*005) to reactive package id.</summary>
    internal static readonly ImmutableDictionary<string, string> ReactivePackageByDiagnosticId =
        InterfaceProxyDomains.ToImmutableDictionary(
            d => d.MissingReactivePackageDiagnosticId,
            d => d.ReactivePackageName,
            StringComparer.Ordinal);

    /// <summary>Backend package id (R3/Reactive/Primitives) to its domain.</summary>
    internal static readonly ImmutableDictionary<string, ProxyDomainDefinition> DomainByBackendPackageId =
        ConsumerPackageConflictDomains
            .SelectMany(d => d.BackendPackageIds.Select(id => (id, d)))
            .ToImmutableDictionary(pair => pair.id, pair => pair.d, StringComparer.Ordinal);

    /// <summary>Flat list of every domain's three backend package ids.</summary>
    internal static readonly ImmutableArray<string> AllBackendPackageIds =
        ConsumerPackageConflictDomains.SelectMany(d => d.BackendPackageIds).ToImmutableArray();

    internal static bool TryGetByMissingRuntimeDiagnosticId(string diagnosticId, out ProxyDomainDefinition domain)
    {
        foreach (var candidate in InterfaceProxyDomains)
        {
            if (candidate.MissingRuntimePackageDiagnosticId == diagnosticId)
            {
                domain = candidate;
                return true;
            }
        }

        domain = null!;
        return false;
    }

    internal static bool TryGetByDiagnosticId(string diagnosticId, out ProxyDomainDefinition domain)
    {
        foreach (var candidate in MemberBoundaryDomains)
        {
            if (candidate.MissingBoundaryDiagnosticId == diagnosticId
                || candidate.MemberShapeMismatchDiagnosticId == diagnosticId)
            {
                domain = candidate;
                return true;
            }
        }

        domain = null!;
        return false;
    }

    internal static bool TryGetDomainByBackendPackageId(string packageId, out ProxyDomainDefinition domain) =>
        DomainByBackendPackageId.TryGetValue(packageId, out domain!);

    internal static ProxyDomainDefinition Get(DomainKind kind) =>
        kind switch
        {
            DomainKind.SignalR => SignalR,
            DomainKind.Mqtt => Mqtt,
            DomainKind.WebSocket => WebSocket,
            DomainKind.Grpc => Grpc,
            DomainKind.Sse => Sse,
            DomainKind.Nats => Nats,
            DomainKind.Postgres => Postgres,
            DomainKind.Redis => Redis,
            DomainKind.RestApi => RestApi,
            DomainKind.Events => Events,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
