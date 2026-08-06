using Microsoft.CodeAnalysis;

namespace Observables.Analyzers;

internal static class ProxyDomainCatalog
{
    internal sealed class ProxyDomain
    {
        internal ProxyDomain(
            string displayName,
            string interfaceMarkerMetadataName,
            string reactiveAdapterMetadataName,
            DiagnosticDescriptor emptyInterfaceDescriptor,
            IReadOnlyList<BoundaryAttributeSuggestion> methodAttributes,
            IReadOnlyList<BoundaryAttributeSuggestion> propertyAttributes)
        {
            DisplayName = displayName;
            InterfaceMarkerMetadataName = interfaceMarkerMetadataName;
            ReactiveAdapterMetadataName = reactiveAdapterMetadataName;
            EmptyInterfaceDescriptor = emptyInterfaceDescriptor;
            MethodAttributes = methodAttributes;
            PropertyAttributes = propertyAttributes;
        }

        internal string DisplayName { get; }
        internal string InterfaceMarkerMetadataName { get; }
        internal string ReactiveAdapterMetadataName { get; }
        internal DiagnosticDescriptor EmptyInterfaceDescriptor { get; }
        internal IReadOnlyList<BoundaryAttributeSuggestion> MethodAttributes { get; }
        internal IReadOnlyList<BoundaryAttributeSuggestion> PropertyAttributes { get; }

        /// <summary>
        /// NuGet / assembly id for the System.Reactive bridge package
        /// (<c>Observables.{DisplayName}.Reactive</c>).
        /// </summary>
        internal string ReactiveAssemblyName => $"Observables.{DisplayName}.Reactive";
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
    }

    internal static readonly ProxyDomain SignalR = new(
        displayName: "SignalR",
        interfaceMarkerMetadataName: "Observables.SignalR.HubAttribute",
        reactiveAdapterMetadataName: "Observables.SignalR.Reactive.SystemReactiveSignalRAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyHubInterface,
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

    internal static readonly ProxyDomain Mqtt = new(
        displayName: "Mqtt",
        interfaceMarkerMetadataName: "Observables.Mqtt.MqttAttribute",
        reactiveAdapterMetadataName: "Observables.Mqtt.Reactive.SystemReactiveMqttAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyMqttInterface,
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("MqttPublish", "MqttPublish"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("MqttSubscribe", "MqttSubscribe"),
        ]);

    internal static readonly ProxyDomain WebSocket = new(
        displayName: "WebSocket",
        interfaceMarkerMetadataName: "Observables.WebSocket.WebSocketAttribute",
        reactiveAdapterMetadataName: "Observables.WebSocket.Reactive.SystemReactiveWebSocketAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyWebSocketInterface,
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

    internal static readonly ProxyDomain Grpc = new(
        displayName: "Grpc",
        interfaceMarkerMetadataName: "Observables.Grpc.GrpcAttribute",
        reactiveAdapterMetadataName: "Observables.Grpc.Reactive.SystemReactiveGrpcAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyGrpcInterface,
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("GrpcUnary", "GrpcUnary"),
            new BoundaryAttributeSuggestion("GrpcServerStream", "GrpcServerStream"),
            new BoundaryAttributeSuggestion("GrpcClientStream", "GrpcClientStream"),
            new BoundaryAttributeSuggestion("GrpcDuplex", "GrpcDuplex"),
        ],
        propertyAttributes: []);

    internal static readonly ProxyDomain Sse = new(
        displayName: "Sse",
        interfaceMarkerMetadataName: "Observables.Sse.SseAttribute",
        reactiveAdapterMetadataName: "Observables.Sse.Reactive.SystemReactiveSseAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptySseInterface,
        methodAttributes: [],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("SseEvent", "SseEvent"),
        ]);

    internal static readonly ProxyDomain Nats = new(
        displayName: "Nats",
        interfaceMarkerMetadataName: "Observables.Nats.NatsAttribute",
        reactiveAdapterMetadataName: "Observables.Nats.Reactive.SystemReactiveNatsAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyNatsInterface,
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("NatsPublish", "NatsPublish"),
            new BoundaryAttributeSuggestion("NatsRequest", "NatsRequest"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("NatsSubscribe", "NatsSubscribe"),
        ]);

    internal static readonly ProxyDomain Postgres = new(
        displayName: "Postgres",
        interfaceMarkerMetadataName: "Observables.Postgres.PostgresAttribute",
        reactiveAdapterMetadataName: "Observables.Postgres.Reactive.SystemReactivePostgresAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyPostgresInterface,
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("Notify", "Notify"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("Listen", "Listen"),
        ]);

    internal static readonly ProxyDomain Redis = new(
        displayName: "Redis",
        interfaceMarkerMetadataName: "Observables.Redis.RedisAttribute",
        reactiveAdapterMetadataName: "Observables.Redis.Reactive.SystemReactiveRedisAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyRedisInterface,
        methodAttributes:
        [
            new BoundaryAttributeSuggestion("RedisPublish", "RedisPublish"),
        ],
        propertyAttributes:
        [
            new BoundaryAttributeSuggestion("RedisSubscribe", "RedisSubscribe"),
        ]);

    internal static readonly ProxyDomain RestApi = new(
        displayName: "RestAPI",
        interfaceMarkerMetadataName: "Observables.RestAPI.RestApiAttribute",
        reactiveAdapterMetadataName: "Observables.RestAPI.Reactive.SystemReactiveObservableAdapter",
        emptyInterfaceDescriptor: DiagnosticDescriptors.EmptyRestApiInterface,
        methodAttributes: [],
        propertyAttributes: []);

    internal static readonly IReadOnlyList<ProxyDomain> InterfaceProxyDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

    internal static readonly IReadOnlyList<ProxyDomain> ReactiveConflictDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

    internal static readonly string[] RestApiHttpMethodNames =
        ["Get", "Post", "Put", "Delete", "Patch", "Head", "Options"];

    internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

    internal static int CountPublicInstanceMembers(INamedTypeSymbol interfaceSymbol)
    {
        var count = 0;
        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic)
            {
                continue;
            }

            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol)
            {
                count++;
            }
        }

        return count;
    }

    internal static ProxyDomain? TryGetInterfaceProxyDomain(INamedTypeSymbol interfaceSymbol, Compilation compilation)
    {
        foreach (var domain in InterfaceProxyDomains)
        {
            var marker = compilation.GetTypeByMetadataName(domain.InterfaceMarkerMetadataName);
            if (marker is not null && HasAttribute(interfaceSymbol, marker))
            {
                return domain;
            }
        }

        return null;
    }
}
