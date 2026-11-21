using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Observables.Roslyn.Shared;

namespace Observables.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OpenGenericProxyInterfaceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.OpenGenericProxyInterface,
            DiagnosticDescriptors.GenericIoProxyMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeInterface, SymbolKind.NamedType);
    }

    static void AnalyzeInterface(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceSymbol)
        {
            return;
        }

        var domain = ProxyDomainCatalog.TryGetInterfaceProxyDomain(interfaceSymbol, context.Compilation);
        if (domain is null)
        {
            return;
        }

        var location = interfaceSymbol.Locations.FirstOrDefault() ?? Location.None;
        if (IsOpenGeneric(interfaceSymbol)
            && (!domain.Definition.SupportsOpenGenericInterfaces
                || (interfaceSymbol.ContainingType is { } container && IsOpenGeneric(container))))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericProxyInterface,
                location,
                interfaceSymbol.Name));
            return;
        }

        if (!ProxyDomainTable.MemberBoundaryDomains.Contains(domain.Definition))
        {
            return;
        }

        foreach (var contract in new[] { interfaceSymbol }.Concat(interfaceSymbol.AllInterfaces))
        {
            foreach (var method in contract.GetMembers().OfType<IMethodSymbol>())
            {
                if (method is not
                    {
                        MethodKind: MethodKind.Ordinary,
                        IsAbstract: true,
                        IsStatic: false,
                        DeclaredAccessibility: Accessibility.Public,
                        Arity: > 0,
                    })
                {
                    continue;
                }

                var methodLocation = SymbolEqualityComparer.Default.Equals(contract, interfaceSymbol)
                    ? method.Locations.FirstOrDefault() ?? location
                    : location;
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GenericIoProxyMethod,
                    methodLocation,
                    interfaceSymbol.Name,
                    method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            }
        }
    }

    internal static bool IsOpenGeneric(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.TypeParameters.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
