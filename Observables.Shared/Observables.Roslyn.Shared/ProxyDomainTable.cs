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
    }

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
            IReadOnlyList<BoundaryAttributeSuggestion> propertyAttributes)
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

        /// <summary>Reactive bridge assembly name; equals <see cref="ReactivePackageName"/> by repository convention.</summary>
        internal string ReactiveAssemblyName => $"Observables.{DisplayName}.Reactive";

        internal string ReactivePackageName => ReactiveAssemblyName;

        internal string R3PackageName => $"Observables.{DisplayName}.R3";

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
        propertyAttributes: []);

    /// <summary>
    /// Domains with interface markers (including RestAPI). Used by empty-interface analysis and OBS0001.
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> InterfaceProxyDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

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

    /// <summary>Reactive package id to R3 package id (the conflicting backend switch target).</summary>
    internal static readonly ImmutableDictionary<string, string> R3PackageByReactivePackageId =
        InterfaceProxyDomains.ToImmutableDictionary(
            d => d.ReactivePackageName,
            d => d.R3PackageName,
            StringComparer.Ordinal);

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
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
