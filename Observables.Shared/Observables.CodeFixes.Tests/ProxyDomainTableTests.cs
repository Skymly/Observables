using Observables.Roslyn.Shared;

namespace Observables.CodeFixes.Tests;

public sealed class ProxyDomainTableTests
{
    public static TheoryData<string, string> MissingBoundaryCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var domain in ProxyDomainTable.MemberBoundaryDomains)
        {
            data.Add(domain.MissingBoundaryDiagnosticId!, domain.Kind.ToString());
        }

        return data;
    }

    public static TheoryData<string, string> ShapeMismatchCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var domain in ProxyDomainTable.MemberBoundaryDomains)
        {
            data.Add(domain.MemberShapeMismatchDiagnosticId!, domain.Kind.ToString());
        }

        return data;
    }

    public static TheoryData<string, string, string?> DefaultAttributeCases()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var domain in ProxyDomainTable.MemberBoundaryDomains)
        {
            data.Add(domain.Kind.ToString(), "method", domain.DefaultMethodAttribute("Alerts"));
            data.Add(domain.Kind.ToString(), "property", domain.DefaultPropertyAttribute("Alerts"));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MissingBoundaryCases))]
    public void TryGetDomain_maps_missing_boundary_ids(string diagnosticId, string expectedKind)
    {
        Assert.True(ObservablesMemberDiagnosticIds.TryGetDomain(diagnosticId, out var domain));
        Assert.Equal(expectedKind, domain.ToString());
        Assert.Contains(diagnosticId, ObservablesMemberDiagnosticIds.MissingBoundaryAttribute);
    }

    [Theory]
    [MemberData(nameof(ShapeMismatchCases))]
    public void TryGetDomain_maps_shape_mismatch_ids(string diagnosticId, string expectedKind)
    {
        Assert.True(ObservablesMemberDiagnosticIds.TryGetDomain(diagnosticId, out var domain));
        Assert.Equal(expectedKind, domain.ToString());
        Assert.Contains(diagnosticId, ObservablesMemberDiagnosticIds.MemberShapeMismatch);
    }

    [Fact]
    public void MissingBoundaryAttribute_covers_all_member_boundary_domains()
    {
        Assert.Equal(
            ProxyDomainTable.MemberBoundaryDomains.Select(d => d.MissingBoundaryDiagnosticId!).OrderBy(id => id, StringComparer.Ordinal),
            ObservablesMemberDiagnosticIds.MissingBoundaryAttribute.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(DefaultAttributeCases))]
    public void BoundaryAttributeDefaults_match_catalog(string domainKind, string memberKind, string? expected)
    {
        var domain = Enum.Parse<ObservablesMemberDiagnosticIds.InterfaceProxyDomain>(domainKind);
        var actual = memberKind == "method"
            ? BoundaryAttributeDefaults.MethodAttribute(domain, "Alerts")
            : BoundaryAttributeDefaults.PropertyAttribute(domain, "Alerts");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RequiresMethod_and_RequiresProperty_cover_catalog_attribute_type_names()
    {
        foreach (var name in ProxyDomainTable.MethodAttributeTypeNames)
        {
            Assert.True(BoundaryAttributeDefaults.RequiresMethod(name), name);
            Assert.False(BoundaryAttributeDefaults.RequiresProperty(name), name);
        }

        foreach (var name in ProxyDomainTable.PropertyAttributeTypeNames)
        {
            Assert.True(BoundaryAttributeDefaults.RequiresProperty(name), name);
            Assert.False(BoundaryAttributeDefaults.RequiresMethod(name), name);
        }
    }

    [Fact]
    public void Postgres_Grpc_Sse_Nats_defaults_are_wired()
    {
        Assert.Contains("OBS10001", ObservablesMemberDiagnosticIds.MissingBoundaryAttribute);
        Assert.Contains("OBS10004", ObservablesMemberDiagnosticIds.MemberShapeMismatch);
        Assert.Contains("OBS7001", ObservablesMemberDiagnosticIds.MissingBoundaryAttribute);
        Assert.Contains("OBS7004", ObservablesMemberDiagnosticIds.MemberShapeMismatch);

        Assert.Equal(
            "[Notify(\"Alerts\")]",
            BoundaryAttributeDefaults.MethodAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Postgres,
                "Alerts"));
        Assert.Equal(
            "[Listen(\"Alerts\")]",
            BoundaryAttributeDefaults.PropertyAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Postgres,
                "Alerts"));
        Assert.Equal(
            "[GrpcUnary(\"Alerts\")]",
            BoundaryAttributeDefaults.MethodAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Grpc,
                "Alerts"));
        Assert.Null(
            BoundaryAttributeDefaults.PropertyAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Grpc,
                "Alerts"));
        Assert.Null(
            BoundaryAttributeDefaults.MethodAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Sse,
                "Alerts"));
        Assert.Equal(
            "[SseEvent(\"Alerts\")]",
            BoundaryAttributeDefaults.PropertyAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Sse,
                "Alerts"));
        Assert.Equal(
            "[NatsPublish(\"Alerts\")]",
            BoundaryAttributeDefaults.MethodAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Nats,
                "Alerts"));
        Assert.Equal(
            "[NatsSubscribe(\"Alerts\")]",
            BoundaryAttributeDefaults.PropertyAttribute(
                ObservablesMemberDiagnosticIds.InterfaceProxyDomain.Nats,
                "Alerts"));
    }
}
