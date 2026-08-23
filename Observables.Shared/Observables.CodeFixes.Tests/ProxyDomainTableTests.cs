using Observables.Roslyn.Shared;

namespace Observables.CodeFixes.Tests;

public sealed class ProxyDomainTableTests
{
    public static TheoryData<string, string> MissingBoundaryCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var domain in ProxyDomainTable.MemberBoundaryDomains)
        {
            data.Add(domain.MissingBoundaryDiagnosticId, domain.Kind.ToString());
        }

        return data;
    }

    public static TheoryData<string, string> ShapeMismatchCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var domain in ProxyDomainTable.MemberBoundaryDomains)
        {
            data.Add(domain.MemberShapeMismatchDiagnosticId, domain.Kind.ToString());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MissingBoundaryCases))]
    public void TryGetByDiagnosticId_maps_missing_boundary_ids(string diagnosticId, string expectedKind)
    {
        Assert.True(ProxyDomainTable.TryGetByDiagnosticId(diagnosticId, out var domain));
        Assert.Equal(expectedKind, domain.Kind.ToString());
        Assert.Contains(diagnosticId, ProxyDomainTable.MissingBoundaryDiagnosticIds);
    }

    [Theory]
    [MemberData(nameof(ShapeMismatchCases))]
    public void TryGetByDiagnosticId_maps_shape_mismatch_ids(string diagnosticId, string expectedKind)
    {
        Assert.True(ProxyDomainTable.TryGetByDiagnosticId(diagnosticId, out var domain));
        Assert.Equal(expectedKind, domain.Kind.ToString());
        Assert.Contains(diagnosticId, ProxyDomainTable.MemberShapeMismatchDiagnosticIds);
    }

    [Fact]
    public void MissingBoundaryDiagnosticIds_cover_all_member_boundary_domains()
    {
        Assert.Equal(
            ProxyDomainTable.MemberBoundaryDomains.Select(d => d.MissingBoundaryDiagnosticId).OrderBy(id => id, StringComparer.Ordinal),
            ProxyDomainTable.MissingBoundaryDiagnosticIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    public static TheoryData<string, string, string?> DefaultAttributeCases() =>
        new()
        {
            { "SignalR", "method", "[HubInvoke(\"Alerts\")]" },
            { "SignalR", "property", "[HubOn(\"Alerts\")]" },
            { "Mqtt", "method", "[MqttPublish(\"Alerts\")]" },
            { "Mqtt", "property", "[MqttSubscribe(\"Alerts\")]" },
            { "WebSocket", "method", "[WebSocketSend(\"Alerts\")]" },
            { "WebSocket", "property", "[WebSocketReceive(\"Alerts\")]" },
            { "Grpc", "method", "[GrpcUnary(\"Alerts\")]" },
            { "Grpc", "property", null },
            { "Sse", "method", null },
            { "Sse", "property", "[SseEvent(\"Alerts\")]" },
            { "Nats", "method", "[NatsPublish(\"Alerts\")]" },
            { "Nats", "property", "[NatsSubscribe(\"Alerts\")]" },
            { "Postgres", "method", "[Notify(\"Alerts\")]" },
            { "Postgres", "property", "[Listen(\"Alerts\")]" },
            { "Redis", "method", "[RedisPublish(\"Alerts\")]" },
            { "Redis", "property", "[RedisSubscribe(\"Alerts\")]" },
        };

    [Theory]
    [MemberData(nameof(DefaultAttributeCases))]
    public void Default_boundary_attributes_are_wired(string domainKind, string memberKind, string? expected)
    {
        var definition = ProxyDomainTable.Get(Enum.Parse<ProxyDomainTable.DomainKind>(domainKind));
        var actual = memberKind == "method"
            ? definition.DefaultMethodAttribute("Alerts")
            : definition.DefaultPropertyAttribute("Alerts");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Attribute_type_names_cover_boundary_suggestions_and_are_disjoint()
    {
        Assert.NotEmpty(ProxyDomainTable.MethodAttributeTypeNames);
        Assert.NotEmpty(ProxyDomainTable.PropertyAttributeTypeNames);
        Assert.Empty(ProxyDomainTable.MethodAttributeTypeNames.Intersect(ProxyDomainTable.PropertyAttributeTypeNames));

        foreach (var name in ProxyDomainTable.MethodAttributeTypeNames)
        {
            Assert.EndsWith("Attribute", name);
        }

        foreach (var name in ProxyDomainTable.PropertyAttributeTypeNames)
        {
            Assert.EndsWith("Attribute", name);
        }
    }

    [Fact]
    public void Every_domain_declares_its_full_diagnostic_identity()
    {
        Assert.Equal(
            ["OBS10007", "OBS11007", "OBS3007", "OBS4007", "OBS5007", "OBS6007", "OBS7007", "OBS8007", "OBS9007"],
            ProxyDomainTable.InterfaceProxyDomains
                .Select(d => d.EmptyInterfaceDiagnosticId)
                .OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            ["OBS10002", "OBS11002", "OBS3002", "OBS4002", "OBS5002", "OBS6002", "OBS7002", "OBS8002", "OBS9002"],
            ProxyDomainTable.RuntimePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            ["OBS10005", "OBS11005", "OBS3005", "OBS4005", "OBS5005", "OBS6005", "OBS7005", "OBS8005", "OBS9005"],
            ProxyDomainTable.ReactivePackageByDiagnosticId.Keys.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void Package_ids_follow_naming_convention()
    {
        Assert.Equal(
            ["Observables.Grpc", "Observables.Mqtt", "Observables.Nats", "Observables.Postgres", "Observables.Redis", "Observables.RestAPI", "Observables.SignalR", "Observables.Sse", "Observables.WebSocket"],
            ProxyDomainTable.RuntimePackageByDiagnosticId.Values.OrderBy(id => id, StringComparer.Ordinal));

        foreach (var domain in ProxyDomainTable.InterfaceProxyDomains)
        {
            Assert.Equal($"Observables.{domain.DisplayName}", domain.RuntimePackageName);
            Assert.Equal($"Observables.{domain.DisplayName}.Reactive", domain.ReactivePackageName);
            Assert.Equal($"Observables.{domain.DisplayName}.R3", domain.R3PackageName);
            Assert.Equal(domain.ReactiveAssemblyName, domain.ReactivePackageName);
        }
    }

    [Fact]
    public void R3PackageByReactivePackageId_maps_every_reactive_package()
    {
        foreach (var domain in ProxyDomainTable.InterfaceProxyDomains)
        {
            Assert.True(ProxyDomainTable.R3PackageByReactivePackageId.TryGetValue(
                domain.ReactivePackageName,
                out var r3PackageId));
            Assert.Equal(domain.R3PackageName, r3PackageId);
        }

        Assert.True(ProxyDomainTable.R3PackageByReactivePackageId.TryGetValue(
            "Observables.Redis.Reactive",
            out var redisR3PackageId));
        Assert.Equal("Observables.Redis.R3", redisR3PackageId);
    }

    [Fact]
    public void RestApi_identity_is_in_the_table_but_excluded_from_generic_member_fixes()
    {
        var restApi = ProxyDomainTable.RestApi;
        Assert.Equal("OBS3001", restApi.MissingBoundaryDiagnosticId);
        Assert.Equal("OBS3004", restApi.MemberShapeMismatchDiagnosticId);
        Assert.Equal("OBS3002", restApi.MissingRuntimePackageDiagnosticId);
        Assert.Equal("OBS3005", restApi.MissingReactivePackageDiagnosticId);
        Assert.Equal("OBS3007", restApi.EmptyInterfaceDiagnosticId);

        Assert.Contains(restApi, ProxyDomainTable.InterfaceProxyDomains);
        Assert.DoesNotContain(restApi, ProxyDomainTable.MemberBoundaryDomains);
        Assert.DoesNotContain("OBS3001", ProxyDomainTable.MissingBoundaryDiagnosticIds);
        Assert.False(ProxyDomainTable.TryGetByDiagnosticId("OBS3001", out _));
    }
}
