using Observables.Roslyn.Shared;

namespace Observables.CodeFixes;

internal static class BoundaryAttributeDefaults
{
    internal static string? MethodAttribute(ObservablesMemberDiagnosticIds.InterfaceProxyDomain domain, string memberName) =>
        ProxyDomainTable.Get((ProxyDomainTable.DomainKind)domain).DefaultMethodAttribute(memberName);

    internal static string? PropertyAttribute(ObservablesMemberDiagnosticIds.InterfaceProxyDomain domain, string memberName) =>
        ProxyDomainTable.Get((ProxyDomainTable.DomainKind)domain).DefaultPropertyAttribute(memberName);

    internal static bool RequiresProperty(string attributeName) =>
        ProxyDomainTable.PropertyAttributeTypeNames.Contains(attributeName);

    internal static bool RequiresMethod(string attributeName) =>
        ProxyDomainTable.MethodAttributeTypeNames.Contains(attributeName);
}
