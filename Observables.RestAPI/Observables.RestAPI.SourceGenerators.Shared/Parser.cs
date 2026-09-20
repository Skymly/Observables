using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Observables.RestAPI.Generators;

internal static partial class Parser
{
    public static (
        List<Diagnostic> diagnostics,
        ContextGenerationModel contextGenerationSpec
    ) GenerateInterfaceStubs(
        CSharpCompilation compilation,
        ImmutableArray<MethodDeclarationSyntax> candidateMethods,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken
    )
    {
        if (compilation == null)
            throw new ArgumentNullException(nameof(compilation));

        var wellKnownTypes = new WellKnownTypes(compilation);

        var options = (CSharpParseOptions)compilation.SyntaxTrees[0].Options;

        var disposableInterfaceSymbol = wellKnownTypes.Get(typeof(IDisposable));
        var httpMethodBaseAttributeSymbol = wellKnownTypes.TryGet("Observables.RestAPI.HttpMethodAttribute");

        var diagnostics = new List<Diagnostic>();
        if (httpMethodBaseAttributeSymbol == null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.RestApiCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<InterfaceModel>()));
        }

        var interfaceToNullableEnabledMap = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
        var methodSymbols = new List<IMethodSymbol>();
        foreach (var group in candidateMethods.GroupBy(m => m.SyntaxTree))
        {
            var model = compilation.GetSemanticModel(group.Key);
            foreach (var method in group)
            {
                var methodSymbol = model.GetDeclaredSymbol(method, cancellationToken: cancellationToken);
                if (!IsHttpMethodAttribute(methodSymbol, httpMethodBaseAttributeSymbol))
                    continue;

                var isAnnotated = model.GetNullableContext(method.SpanStart) == NullableContext.Enabled;
                interfaceToNullableEnabledMap[methodSymbol!.ContainingType] = isAnnotated;
                methodSymbols.Add(methodSymbol!);
            }
        }

        var interfaces = methodSymbols
            .GroupBy<IMethodSymbol, INamedTypeSymbol>(m => m.ContainingType, SymbolEqualityComparer.Default)
            .ToDictionary<IGrouping<INamedTypeSymbol, IMethodSymbol>, INamedTypeSymbol, List<IMethodSymbol>>(
                g => g.Key, v => [.. v], SymbolEqualityComparer.Default);

        foreach (var group in candidateInterfaces.GroupBy(i => i.SyntaxTree))
        {
            var model = compilation.GetSemanticModel(group.Key);
            foreach (var iface in group)
            {
                var ifaceSymbol = model.GetDeclaredSymbol(iface, cancellationToken: cancellationToken);
                if (ifaceSymbol is null || interfaces.ContainsKey(ifaceSymbol))
                    continue;

                var hasDerivedHttpMethod = ifaceSymbol
                    .AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                    .Any(m => IsHttpMethodAttribute(m, httpMethodBaseAttributeSymbol));

                if (hasDerivedHttpMethod)
                {
                    interfaces.Add(ifaceSymbol, []);
                    interfaceToNullableEnabledMap[ifaceSymbol] = model.GetNullableContext(iface.SpanStart) == NullableContext.Enabled;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (interfaces.Count == 0)
            return (diagnostics, new ContextGenerationModel(ImmutableEquatableArray.Empty<InterfaceModel>()));

        var supportsNullable = options.LanguageVersion >= LanguageVersion.CSharp8;
        var keyCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var interfaceModels = new List<InterfaceModel>();

        foreach (var group in interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keyName = group.Key.Name;
            int value;
            while (keyCount.TryGetValue(keyName, out value))
                keyName = $"{keyName}{++value}";
            keyCount[keyName] = value;
            var fileName = $"{keyName}.g.cs";

            interfaceModels.Add(ProcessInterface(
                fileName, diagnostics, group.Key, group.Value,
                disposableInterfaceSymbol, httpMethodBaseAttributeSymbol,
                supportsNullable, interfaceToNullableEnabledMap[group.Key], wellKnownTypes));
        }

        return (diagnostics, new ContextGenerationModel(interfaceModels.ToImmutableEquatableArray()));
    }

    static InterfaceModel ProcessInterface(
        string fileName, List<Diagnostic> diagnostics, INamedTypeSymbol interfaceSymbol,
        List<IMethodSymbol> httpMethodSymbols, ISymbol disposableInterfaceSymbol,
        INamedTypeSymbol httpMethodBaseAttributeSymbol, bool supportsNullable, bool nullableEnabled,
        WellKnownTypes wellKnownTypes)
    {
        var className = interfaceSymbol.ToDisplayString();
        var lastDot = className.LastIndexOf('.');
        if (lastDot > 0) className = className.Substring(lastDot + 1);
        var classDeclaration = $"{interfaceSymbol.ContainingType?.Name}{className}";
        var classSuffix = $"{interfaceSymbol.ContainingType?.Name}{interfaceSymbol.Name}";
        var ns = interfaceSymbol.ContainingNamespace?.ToDisplayString();
        if (interfaceSymbol.ContainingNamespace is { IsGlobalNamespace: true }) ns = string.Empty;
        ns = ns!.Replace(".", "_");
        var interfaceDisplayName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var nonHttpMethods = interfaceSymbol.GetMembers().OfType<IMethodSymbol>()
            .Except(httpMethodSymbols, SymbolEqualityComparer.Default).Cast<IMethodSymbol>().ToArray();

        var derivedMethods = interfaceSymbol.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>()).ToList();

        var disposeMethod = derivedMethods.Find(m =>
            m.ContainingType?.Equals(disposableInterfaceSymbol, SymbolEqualityComparer.Default) == true);
        if (disposeMethod != null) derivedMethods.Remove(disposeMethod);

        var derivedHttpMethods = derivedMethods
            .Where(m => IsHttpMethodAttribute(m, httpMethodBaseAttributeSymbol)).ToArray();
        var derivedNonHttpMethods = derivedMethods
            .Except(derivedHttpMethods, SymbolEqualityComparer.Default).Cast<IMethodSymbol>().ToArray();

        if (derivedNonHttpMethods.Length > 0)
        {
            var explicitImpls = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (var member in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
                foreach (var bm in member.ExplicitInterfaceImplementations)
                    explicitImpls.Add(bm.OriginalDefinition ?? bm);
            if (explicitImpls.Count > 0)
                derivedNonHttpMethods = derivedNonHttpMethods
                    .Where(m => !explicitImpls.Contains(m.OriginalDefinition ?? m)).ToArray();
        }

        var httpMethodsArray = httpMethodSymbols
            .Select(m => ParseMethod(m, true, httpMethodBaseAttributeSymbol, wellKnownTypes, diagnostics))
            .ToImmutableEquatableArray();
        var derivedHttpMethodsArray = derivedHttpMethods
            .Select(m => ParseMethod(m, false, httpMethodBaseAttributeSymbol, wellKnownTypes, diagnostics))
            .ToImmutableEquatableArray();

        var nonHttpMethodModelList = new List<MethodModel>();
        foreach (var method in nonHttpMethods)
        {
            if (method.IsStatic || method.MethodKind == MethodKind.PropertyGet
                || method.MethodKind == MethodKind.PropertySet || !method.IsAbstract) continue;
            nonHttpMethodModelList.Add(ParseNonHttpMethod(method, wellKnownTypes, diagnostics, isDerived: false));
        }
        foreach (var method in derivedNonHttpMethods)
        {
            if (method.IsStatic || method.MethodKind == MethodKind.PropertyGet
                || method.MethodKind == MethodKind.PropertySet || !method.IsAbstract) continue;
            nonHttpMethodModelList.Add(ParseNonHttpMethod(method, wellKnownTypes, diagnostics, isDerived: true));
        }

        var constraints = GenerateConstraints(interfaceSymbol.TypeParameters, false);
        var nullability = (supportsNullable, nullableEnabled) switch
        {
            (false, _) => Nullability.None,
            (true, true) => Nullability.Enabled,
            (true, false) => Nullability.Disabled,
        };
        return new InterfaceModel(fileName, className, ns, classDeclaration, interfaceDisplayName,
            classSuffix, constraints, nonHttpMethodModelList.ToImmutableEquatableArray(),
            httpMethodsArray, derivedHttpMethodsArray, nullability, disposeMethod != null);
    }

    static MethodModel ParseNonHttpMethod(
        IMethodSymbol methodSymbol,
        WellKnownTypes wellKnownTypes,
        List<Diagnostic> diagnostics,
        bool isDerived)
    {
        foreach (var location in methodSymbol.Locations)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.InvalidRestApiMember, location,
                methodSymbol.ContainingType.Name, methodSymbol.Name));
        }

        var explicitImpl = methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
        var containingTypeSymbol = explicitImpl?.ContainingType ?? methodSymbol.ContainingType;
        var containingType = containingTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var declaredBaseName = methodSymbol.Name;
        var lastDot = declaredBaseName.LastIndexOf('.');
        if (lastDot >= 0) declaredBaseName = declaredBaseName.Substring(lastDot + 1);

        if (methodSymbol.TypeParameters.Length > 0)
        {
            var typeParams = string.Join(", ", methodSymbol.TypeParameters
                .Select(tp => tp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            declaredBaseName += $"<{typeParams}>";
        }

        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var returnClassification = RestApiReturnTypeClassifier.Classify(
            methodSymbol.ReturnType,
            methodSymbol,
            wellKnownTypes,
            diagnostics);

        var parameters = methodSymbol.Parameters.Select(ParseParameter).ToImmutableEquatableArray();
        var isExplicit = isDerived || explicitImpl is not null;
        var constraints = GenerateConstraints(methodSymbol.TypeParameters, isExplicit);

        return new MethodModel(methodSymbol.Name, returnType, containingType, declaredBaseName,
            returnClassification.Info, parameters, constraints, isExplicit,
            Spec: RestApiMethodSpecModel.Empty,
            IsApiResponse: returnClassification.IsApiResponse,
            ReturnResultType: returnClassification.ReturnResultType,
            DeserializedResultType: returnClassification.DeserializedResultType);
    }

    static bool IsHttpMethodAttribute(IMethodSymbol? methodSymbol, INamedTypeSymbol httpMethodAttribute) =>
        methodSymbol?.GetAttributes().Any(ad => ad.AttributeClass?.InheritsFromOrEquals(httpMethodAttribute) == true) == true;

    static bool IsCancellationTokenParameter(IParameterSymbol parameter) =>
        parameter.Type.Name == "CancellationToken"
        && parameter.Type.ContainingNamespace?.ToDisplayString() == "System.Threading";

    static ImmutableEquatableArray<TypeConstraint> GenerateConstraints(
        ImmutableArray<ITypeParameterSymbol> typeParameters, bool isOverrideOrExplicitImplementation) =>
        typeParameters.Select(tp => ParseConstraintsForTypeParameter(tp, isOverrideOrExplicitImplementation))
            .ToImmutableEquatableArray();

    static TypeConstraint ParseConstraintsForTypeParameter(ITypeParameterSymbol tp, bool isOverrideOrExplicit)
    {
        var known = KnownTypeConstraint.None;
        if (tp.HasReferenceTypeConstraint) known |= KnownTypeConstraint.Class;
        if (tp.HasUnmanagedTypeConstraint && !isOverrideOrExplicit) known |= KnownTypeConstraint.Unmanaged;
        if (tp.HasValueTypeConstraint && !tp.HasUnmanagedTypeConstraint) known |= KnownTypeConstraint.Struct;
        if (tp.HasNotNullConstraint && !isOverrideOrExplicit) known |= KnownTypeConstraint.NotNull;

        var constraints = ImmutableEquatableArray<string>.Empty;
        if (!isOverrideOrExplicit)
            constraints = tp.ConstraintTypes.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToImmutableEquatableArray();
        if (tp.HasConstructorConstraint && !isOverrideOrExplicit) known |= KnownTypeConstraint.New;

        return new TypeConstraint(tp.Name, tp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), known, constraints);
    }

    static ParameterModel ParseParameter(IParameterSymbol param)
    {
        var annotation = !param.Type.IsValueType && param.NullableAnnotation == NullableAnnotation.Annotated;
        var paramType = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isGeneric = ContainsTypeParameter(param.Type);
        return new ParameterModel(param.MetadataName, paramType, annotation, isGeneric);
    }

    static bool ContainsTypeParameter(ITypeSymbol symbol)
    {
        if (symbol is ITypeParameterSymbol) return true;
        if (symbol is not INamedTypeSymbol { TypeParameters.Length: > 0 } namedType) return false;
        foreach (var typeArg in namedType.TypeArguments)
            if (ContainsTypeParameter(typeArg)) return true;
        return false;
    }

    static MethodModel ParseMethod(IMethodSymbol methodSymbol, bool isImplicitInterface,
        INamedTypeSymbol httpMethodBaseAttributeSymbol, WellKnownTypes wellKnownTypes, List<Diagnostic> diagnostics)
    {
        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var explicitImpl = methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
        var containingTypeSymbol = explicitImpl?.ContainingType ?? methodSymbol.ContainingType;
        var containingType = containingTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var declaredBaseName = methodSymbol.Name;
        var lastDot = declaredBaseName.LastIndexOf('.');
        if (lastDot >= 0) declaredBaseName = declaredBaseName.Substring(lastDot + 1);
        if (methodSymbol.TypeParameters.Length > 0)
        {
            var typeParams = string.Join(", ", methodSymbol.TypeParameters
                .Select(tp => tp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            declaredBaseName += $"<{typeParams}>";
        }

        var returnClassification = RestApiReturnTypeClassifier.Classify(
            methodSymbol.ReturnType,
            methodSymbol,
            wellKnownTypes,
            diagnostics);

        var http = ParseHttpMethod(methodSymbol, httpMethodBaseAttributeSymbol, diagnostics);

        var isExplicit = explicitImpl is not null;
        var constraints = GenerateConstraints(methodSymbol.TypeParameters, isExplicit || !isImplicitInterface);

        return new MethodModel(methodSymbol.Name, returnType, containingType, declaredBaseName,
            returnClassification.Info, http.Parameters, constraints, isExplicit,
            Spec: http.Spec,
            CancellationTokenIndex: http.CancellationTokenIndex,
            IsApiResponse: returnClassification.IsApiResponse,
            ReturnResultType: returnClassification.ReturnResultType,
            DeserializedResultType: returnClassification.DeserializedResultType);
    }
}
