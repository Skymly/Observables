using Microsoft.CodeAnalysis;
using Observables.Roslyn.Shared;

namespace Observables.Analyzers;

internal static class ProxyDomainCatalog
{
    internal sealed class ProxyDomain
    {
        internal ProxyDomain(
            ProxyDomainTable.ProxyDomainDefinition definition,
            DiagnosticDescriptor emptyInterfaceDescriptor)
        {
            Definition = definition;
            EmptyInterfaceDescriptor = emptyInterfaceDescriptor;
        }

        internal ProxyDomainTable.ProxyDomainDefinition Definition { get; }
        internal string DisplayName => Definition.DisplayName;
        internal string InterfaceMarkerMetadataName => Definition.InterfaceMarkerMetadataName;
        internal string ReactiveAdapterMetadataName => Definition.ReactiveAdapterMetadataName;
        internal DiagnosticDescriptor EmptyInterfaceDescriptor { get; }
        internal IReadOnlyList<ProxyDomainTable.BoundaryAttributeSuggestion> MethodAttributes =>
            Definition.MethodAttributes;
        internal IReadOnlyList<ProxyDomainTable.BoundaryAttributeSuggestion> PropertyAttributes =>
            Definition.PropertyAttributes;
        internal string ReactiveAssemblyName => Definition.ReactiveAssemblyName;
    }

    internal static readonly ProxyDomain SignalR = new(
        ProxyDomainTable.SignalR,
        DiagnosticDescriptors.EmptyHubInterface);

    internal static readonly ProxyDomain Mqtt = new(
        ProxyDomainTable.Mqtt,
        DiagnosticDescriptors.EmptyMqttInterface);

    internal static readonly ProxyDomain WebSocket = new(
        ProxyDomainTable.WebSocket,
        DiagnosticDescriptors.EmptyWebSocketInterface);

    internal static readonly ProxyDomain Grpc = new(
        ProxyDomainTable.Grpc,
        DiagnosticDescriptors.EmptyGrpcInterface);

    internal static readonly ProxyDomain Sse = new(
        ProxyDomainTable.Sse,
        DiagnosticDescriptors.EmptySseInterface);

    internal static readonly ProxyDomain Nats = new(
        ProxyDomainTable.Nats,
        DiagnosticDescriptors.EmptyNatsInterface);

    internal static readonly ProxyDomain Postgres = new(
        ProxyDomainTable.Postgres,
        DiagnosticDescriptors.EmptyPostgresInterface);

    internal static readonly ProxyDomain Redis = new(
        ProxyDomainTable.Redis,
        DiagnosticDescriptors.EmptyRedisInterface);

    internal static readonly ProxyDomain RestApi = new(
        ProxyDomainTable.RestApi,
        DiagnosticDescriptors.EmptyRestApiInterface);

    internal static readonly IReadOnlyList<ProxyDomain> InterfaceProxyDomains =
        [SignalR, Mqtt, WebSocket, Grpc, Sse, Nats, Postgres, Redis, RestApi];

    internal static readonly IReadOnlyList<ProxyDomain> ReactiveConflictDomains = InterfaceProxyDomains;

    internal static readonly string[] RestApiHttpMethodNames = ProxyDomainTable.RestApiHttpMethodNames;

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
