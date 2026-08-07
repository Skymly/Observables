using System.Collections.Immutable;

namespace Observables.Roslyn.Shared;

/// <summary>
/// Pure proxy-domain catalog shared by Analyzers and CodeFixes (linked into both assemblies).
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
            string? missingBoundaryDiagnosticId,
            string? memberShapeMismatchDiagnosticId,
            IReadOnlyList<BoundaryAttributeSuggestion> methodAttributes,
            IReadOnlyList<BoundaryAttributeSuggestion> propertyAttributes)
        {
            Kind = kind;
            DisplayName = displayName;
            InterfaceMarkerMetadataName = interfaceMarkerMetadataName;
            ReactiveAdapterMetadataName = reactiveAdapterMetadataName;
            MissingBoundaryDiagnosticId = missingBoundaryDiagnosticId;
            MemberShapeMismatchDiagnosticId = memberShapeMismatchDiagnosticId;
            MethodAttributes = methodAttributes;
            PropertyAttributes = propertyAttributes;
        }

        internal DomainKind Kind { get; }
        internal string DisplayName { get; }
        internal string InterfaceMarkerMetadataName { get; }
        internal string ReactiveAdapterMetadataName { get; }
        internal string? MissingBoundaryDiagnosticId { get; }
        internal string? MemberShapeMismatchDiagnosticId { get; }
        internal IReadOnlyList<BoundaryAttributeSuggestion> MethodAttributes { get; }
        internal IReadOnlyList<BoundaryAttributeSuggestion> PropertyAttributes { get; }

        internal string ReactiveAssemblyName => $"Observables.{DisplayName}.Reactive";

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
        missingBoundaryDiagnosticId: "OBS4001",
        memberShapeMismatchDiagnosticId: "OBS4004",
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
        missingBoundaryDiagnosticId: "OBS5001",
        memberShapeMismatchDiagnosticId: "OBS5004",
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
        missingBoundaryDiagnosticId: "OBS6001",
        memberShapeMismatchDiagnosticId: "OBS6004",
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
        missingBoundaryDiagnosticId: "OBS7001",
        memberShapeMismatchDiagnosticId: "OBS7004",
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
        missingBoundaryDiagnosticId: "OBS8001",
        memberShapeMismatchDiagnosticId: "OBS8004",
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
        missingBoundaryDiagnosticId: "OBS9001",
        memberShapeMismatchDiagnosticId: "OBS9004",
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
        missingBoundaryDiagnosticId: "OBS10001",
        memberShapeMismatchDiagnosticId: "OBS10004",
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
        missingBoundaryDiagnosticId: "OBS11001",
        memberShapeMismatchDiagnosticId: "OBS11004",
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("RedisPublish", "RedisPublish"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("RedisSubscribe", "RedisSubscribe"),
        ]);

    internal static readonly ProxyDomainDefinition RestApi = new(
        DomainKind.RestApi,
        displayName: "RestAPI",
        interfaceMarkerMetadataName: "Observables.RestAPI.RestApiAttribute",
        reactiveAdapterMetadataName: "Observables.RestAPI.Reactive.SystemReactiveObservableAdapter",
        missingBoundaryDiagnosticId: null,
        memberShapeMismatchDiagnosticId: null,
        methodAttributes: [],
        propertyAttributes: []);

    /// <summary>
    /// Domains with interface markers (including RestAPI). Used by empty-interface analysis and OBS0001.
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> InterfaceProxyDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

    /// <summary>
    /// Domains that participate in member missing-attribute / shape CodeFixes (excludes RestAPI).
    /// </summary>
    internal static readonly IReadOnlyList<ProxyDomainDefinition> MemberBoundaryDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis];

    internal static readonly string[] RestApiHttpMethodNames =
        ["Get", "Post", "Put", "Delete", "Patch", "Head", "Options"];

    internal static readonly ImmutableArray<string> MissingBoundaryDiagnosticIds =
        MemberBoundaryDomains
            .Select(d => d.MissingBoundaryDiagnosticId!)
            .ToImmutableArray();

    internal static readonly ImmutableArray<string> MemberShapeMismatchDiagnosticIds =
        MemberBoundaryDomains
            .Select(d => d.MemberShapeMismatchDiagnosticId!)
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
