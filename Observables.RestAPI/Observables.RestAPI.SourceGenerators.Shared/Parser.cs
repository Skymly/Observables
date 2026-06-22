using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Observables.RestAPI.Generators;

internal static class Parser
{
    public static (
        List<Diagnostic> diagnostics,
        ContextGenerationModel contextGenerationSpec
    ) GenerateInterfaceStubs(
        CSharpCompilation compilation,
        string? RestApiInternalNamespace,
        ImmutableArray<MethodDeclarationSyntax> candidateMethods,
        ImmutableArray<InterfaceDeclarationSyntax> candidateInterfaces,
        CancellationToken cancellationToken
    )
    {
        if (compilation == null)
            throw new ArgumentNullException(nameof(compilation));

        var wellKnownTypes = new WellKnownTypes(compilation);

        RestApiInternalNamespace = $"{RestApiInternalNamespace ?? string.Empty}RestApiInternalGenerated";
        RestApiInternalNamespace = RestApiInternalNamespace.Replace('-', '_').Replace('@', '_');

        var options = (CSharpParseOptions)compilation.SyntaxTrees[0].Options;

        var disposableInterfaceSymbol = wellKnownTypes.Get(typeof(IDisposable));
        var httpMethodBaseAttributeSymbol = wellKnownTypes.TryGet("Observables.RestAPI.HttpMethodAttribute");

        var diagnostics = new List<Diagnostic>();
        if (httpMethodBaseAttributeSymbol == null)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.RestApiCoreNotReferenced, null));
            return (diagnostics, new ContextGenerationModel(RestApiInternalNamespace, ImmutableEquatableArray.Empty<InterfaceModel>()));
        }

        var interfaceToNullableEnabledMap = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
        var methodSymbols = new List<IMethodSymbol>();
        foreach (var group in candidateMethods.GroupBy(m => m.SyntaxTree))
        {
            var model = compilation.GetSemanticModel(group.Key);
            foreach (var method in group)
            {
                var methodSymbol = model.GetDeclaredSymbol(method, cancellationToken: cancellationToken);
                if (!IsRefitMethod(methodSymbol, httpMethodBaseAttributeSymbol))
                    continue;

                var isAnnotated = compilation.Options.NullableContextOptions == NullableContextOptions.Enable
                    || model.GetNullableContext(method.SpanStart) == NullableContext.Enabled;
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

                var hasDerivedRefit = ifaceSymbol
                    .AllInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>())
                    .Any(m => IsRefitMethod(m, httpMethodBaseAttributeSymbol));

                if (hasDerivedRefit)
                {
                    interfaces.Add(ifaceSymbol, []);
                    interfaceToNullableEnabledMap[ifaceSymbol] = model.GetNullableContext(iface.SpanStart) == NullableContext.Enabled;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (interfaces.Count == 0)
            return (diagnostics, new ContextGenerationModel(RestApiInternalNamespace, ImmutableEquatableArray.Empty<InterfaceModel>()));

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

        return (diagnostics, new ContextGenerationModel(RestApiInternalNamespace, interfaceModels.ToImmutableEquatableArray()));
    }

    static InterfaceModel ProcessInterface(
        string fileName, List<Diagnostic> diagnostics, INamedTypeSymbol interfaceSymbol,
        List<IMethodSymbol> refitMethods, ISymbol disposableInterfaceSymbol,
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
        ns = ns!.Replace(".", "");
        var interfaceDisplayName = interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var nonRefitMethods = interfaceSymbol.GetMembers().OfType<IMethodSymbol>()
            .Except(refitMethods, SymbolEqualityComparer.Default).Cast<IMethodSymbol>().ToArray();

        var derivedMethods = interfaceSymbol.AllInterfaces
            .SelectMany(i => i.GetMembers().OfType<IMethodSymbol>()).ToList();

        var disposeMethod = derivedMethods.Find(m =>
            m.ContainingType?.Equals(disposableInterfaceSymbol, SymbolEqualityComparer.Default) == true);
        if (disposeMethod != null) derivedMethods.Remove(disposeMethod);

        var derivedRefitMethods = derivedMethods
            .Where(m => IsRefitMethod(m, httpMethodBaseAttributeSymbol)).ToArray();
        var derivedNonRefitMethods = derivedMethods
            .Except(derivedRefitMethods, SymbolEqualityComparer.Default).Cast<IMethodSymbol>().ToArray();

        if (derivedNonRefitMethods.Length > 0)
        {
            var explicitImpls = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (var member in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
                foreach (var bm in member.ExplicitInterfaceImplementations)
                    explicitImpls.Add(bm.OriginalDefinition ?? bm);
            if (explicitImpls.Count > 0)
                derivedNonRefitMethods = derivedNonRefitMethods
                    .Where(m => !explicitImpls.Contains(m.OriginalDefinition ?? m)).ToArray();
        }

        var memberNames = interfaceSymbol.GetMembers().Select(x => x.Name).Distinct().ToImmutableEquatableArray();
        var refitMethodsArray = refitMethods
            .Select(m => ParseMethod(m, true, httpMethodBaseAttributeSymbol, wellKnownTypes, diagnostics))
            .ToImmutableEquatableArray();
        var derivedRefitMethodsArray = derivedRefitMethods
            .Select(m => ParseMethod(m, false, httpMethodBaseAttributeSymbol, wellKnownTypes, diagnostics))
            .ToImmutableEquatableArray();

        var nonRefitMethodModelList = new List<MethodModel>();
        foreach (var method in nonRefitMethods)
        {
            if (method.IsStatic || method.MethodKind == MethodKind.PropertyGet
                || method.MethodKind == MethodKind.PropertySet || !method.IsAbstract) continue;
            nonRefitMethodModelList.Add(ParseNonRefitMethod(method, diagnostics, isDerived: false));
        }
        foreach (var method in derivedNonRefitMethods)
        {
            if (method.IsStatic || method.MethodKind == MethodKind.PropertyGet
                || method.MethodKind == MethodKind.PropertySet || !method.IsAbstract) continue;
            nonRefitMethodModelList.Add(ParseNonRefitMethod(method, diagnostics, isDerived: true));
        }

        var constraints = GenerateConstraints(interfaceSymbol.TypeParameters, false);
        var nullability = (supportsNullable, nullableEnabled) switch
        {
            (false, _) => Nullability.None,
            (true, true) => Nullability.Enabled,
            (true, false) => Nullability.Disabled,
        };
        return new InterfaceModel(fileName, className, ns, classDeclaration, interfaceDisplayName,
            classSuffix, constraints, memberNames, nonRefitMethodModelList.ToImmutableEquatableArray(),
            refitMethodsArray, derivedRefitMethodsArray, nullability, disposeMethod != null);
    }

    static MethodModel ParseNonRefitMethod(IMethodSymbol methodSymbol, List<Diagnostic> diagnostics, bool isDerived)
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
        var returnTypeInfo = methodSymbol.ReturnType.MetadataName switch
        {
            "Task" => ReturnTypeInfo.AsyncVoid,
            "Task`1" or "ValueTask`1" => ReturnTypeInfo.AsyncResult,
            "Void" => ReturnTypeInfo.SyncVoid,
            _ => ReturnTypeInfo.Return,
        };

        var parameters = methodSymbol.Parameters.Select(ParseParameter).ToImmutableEquatableArray();
        var isExplicit = isDerived || explicitImpl is not null;
        var constraints = GenerateConstraints(methodSymbol.TypeParameters, isExplicit);

        return new MethodModel(methodSymbol.Name, returnType, containingType, declaredBaseName,
            returnTypeInfo, parameters, constraints, isExplicit);
    }

    static bool IsRefitMethod(IMethodSymbol? methodSymbol, INamedTypeSymbol httpMethodAttribute) =>
        methodSymbol?.GetAttributes().Any(ad => ad.AttributeClass?.InheritsFromOrEquals(httpMethodAttribute) == true) == true;

    static void ValidatePathTemplate(IMethodSymbol methodSymbol, INamedTypeSymbol httpMethodBaseAttributeSymbol, List<Diagnostic> diagnostics)
    {
        AttributeData? httpAttr = null;
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.InheritsFromOrEquals(httpMethodBaseAttributeSymbol) == true) { httpAttr = attr; break; }
        }
        if (httpAttr?.ConstructorArguments is not { Length: 1 } args || args[0].Value is not string path) return;

        var placeholders = ExtractPathPlaceholders(path);
        var paramNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in methodSymbol.Parameters)
            if (!IsCancellationTokenParameter(parameter)) paramNames.Add(parameter.Name);

        if (!placeholders.SetEquals(paramNames))
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.PathParameterMismatch,
                methodSymbol.Locations.FirstOrDefault(), methodSymbol.Name));
    }

    static HashSet<string> ExtractPathPlaceholders(string path)
    {
        var placeholders = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] != '{') continue;
            var close = path.IndexOf('}', i + 1);
            if (close < 0) break;
            var name = path.Substring(i + 1, close - i - 1);
            if (!string.IsNullOrEmpty(name)) placeholders.Add(name);
            i = close;
        }
        return placeholders;
    }

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

        var returnTypeInfo = ClassifyReturnType(methodSymbol.ReturnType, methodSymbol, wellKnownTypes, diagnostics);
        ValidatePathTemplate(methodSymbol, httpMethodBaseAttributeSymbol, diagnostics);

        var httpSemantics = ParseHttpSemantics(methodSymbol, httpMethodBaseAttributeSymbol);
        var parameters = methodSymbol.Parameters
            .Select((p, i) => ParseParameterWithKind(p, i, httpSemantics))
            .ToImmutableEquatableArray();

        var isExplicit = explicitImpl is not null;
        var constraints = GenerateConstraints(methodSymbol.TypeParameters, isExplicit || !isImplicitInterface);
        var (returnResultType, deserializedResultType, isApiResponse) = ExtractReturnTypeInfo(methodSymbol.ReturnType, returnTypeInfo);

        return new MethodModel(methodSymbol.Name, returnType, containingType, declaredBaseName,
            returnTypeInfo, parameters, constraints, isExplicit,
            HttpMethod: httpSemantics.HttpMethod,
            PathFragments: httpSemantics.PathFragments,
            CancellationTokenIndex: httpSemantics.CancellationTokenIndex,
            BodyParameterIndex: httpSemantics.BodyParameterIndex,
            BodySerializationMethod: httpSemantics.BodySerializationMethod,
            BodyBuffered: httpSemantics.BodyBuffered,
            Headers: httpSemantics.Headers,
            IsMultipart: httpSemantics.IsMultipart,
            MultipartBoundary: httpSemantics.MultipartBoundary,
            QueryUriFormat: httpSemantics.QueryUriFormat,
            IsApiResponse: isApiResponse,
            ReturnResultType: returnResultType,
            DeserializedResultType: deserializedResultType);
    }

    static (string ReturnResultType, string DeserializedResultType, bool IsApiResponse) ExtractReturnTypeInfo(
        ITypeSymbol returnType, ReturnTypeInfo returnTypeInfo)
    {
        if (returnTypeInfo == ReturnTypeInfo.SyncVoid || returnTypeInfo == ReturnTypeInfo.AsyncVoid)
            return ("void", "void", false);

        ITypeSymbol? innerType = null;
        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
            innerType = namedType.TypeArguments.FirstOrDefault();

        if (innerType == null)
            return (returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), false);

        var innerDisplay = innerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var isApiResponse = innerType is INamedTypeSymbol innerNamed
            && (innerNamed.Name == "ApiResponse" || innerNamed.Name == "IApiResponse") && innerNamed.IsGenericType;

        if (isApiResponse && innerType is INamedTypeSymbol apiResponseNamed)
        {
            var bodyType = apiResponseNamed.TypeArguments.FirstOrDefault();
            var bodyDisplay = bodyType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
            return (innerDisplay, bodyDisplay, true);
        }

        return (innerDisplay, innerDisplay, false);
    }

    static ReturnTypeInfo ClassifyReturnType(ITypeSymbol returnType, IMethodSymbol methodSymbol,
        WellKnownTypes wellKnownTypes, List<Diagnostic> diagnostics)
    {
        if (returnType.SpecialType == SpecialType.System_Void) return ReturnTypeInfo.SyncVoid;
        if (returnType.MetadataName == "Task") return ReturnTypeInfo.AsyncVoid;

        if (returnType is INamedTypeSymbol { IsGenericType: true } namedType)
        {
            var def = namedType.OriginalDefinition;
            var metadata = def.MetadataName;
            if (metadata is "Task`1" or "ValueTask`1") return ReturnTypeInfo.AsyncResult;

#if RESTAPI_R3
            if (metadata == "Observable`1" && def.ContainingNamespace?.ToDisplayString() == "R3")
                return ReturnTypeInfo.R3Observable;
            if (metadata == "IObservable`1")
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.SystemReactiveNotReferenced,
                    methodSymbol.Locations.FirstOrDefault(), returnType.ToDisplayString()));
                return ReturnTypeInfo.Unsupported;
            }
#elif RESTAPI_SYSTEM_REACTIVE
            if (metadata == "Observable`1" && def.ContainingNamespace?.ToDisplayString() == "R3")
            {
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.UnsupportedReturnType,
                    methodSymbol.Locations.FirstOrDefault(), returnType.ToDisplayString()));
                return ReturnTypeInfo.Unsupported;
            }
            if (metadata == "IObservable`1")
            {
                if (wellKnownTypes.TryGet("Observables.RestAPI.Reactive.SystemReactiveObservableAdapter") != null)
                    return ReturnTypeInfo.SystemReactiveObservable;
                diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.SystemReactiveNotReferenced,
                    methodSymbol.Locations.FirstOrDefault(), returnType.ToDisplayString()));
                return ReturnTypeInfo.Unsupported;
            }
#endif
        }
        return ReturnTypeInfo.Return;
    }

    // ─── HTTP semantic parsing ───────────────────────────────────────────

    class HttpSemantics
    {
        public string HttpMethod { get; set; } = "";
        public ImmutableEquatableArray<PathFragmentModel> PathFragments { get; set; } = ImmutableEquatableArray<PathFragmentModel>.Empty;
        public List<ParameterClassification> ParameterClassifications { get; set; } = new();
        public int? CancellationTokenIndex { get; set; }
        public int? BodyParameterIndex { get; set; }
        public int BodySerializationMethod { get; set; }
        public bool? BodyBuffered { get; set; }
        public ImmutableEquatableArray<string> Headers { get; set; } = ImmutableEquatableArray<string>.Empty;
        public bool IsMultipart { get; set; }
        public string MultipartBoundary { get; set; } = "----MyGreatBoundary";
        public int QueryUriFormat { get; set; }
    }

    class ParameterClassification
    {
        public int Index { get; set; }
        public ParameterKind Kind { get; set; } = ParameterKind.None;
        public string? AliasAs { get; set; }
        public string? HeaderName { get; set; }
        public string? AuthorizeScheme { get; set; }
        public string? PropertyKey { get; set; }
        public string? QueryFormat { get; set; }
        public string? QueryPrefix { get; set; }
        public string QueryDelimiter { get; set; } = ".";
        public bool QueryTreatAsString { get; set; }
        public int QueryCollectionFormat { get; set; }
        public bool QueryIsCollectionFormatSpecified { get; set; }
        public BodySerializationMethod BodySerializationMethod { get; set; } = BodySerializationMethod.Default;
        public bool? BodyBuffered { get; set; }
    }

    static HttpSemantics ParseHttpSemantics(IMethodSymbol methodSymbol, INamedTypeSymbol httpMethodBaseAttributeSymbol)
    {
        var semantics = new HttpSemantics();

        AttributeData? httpAttr = null;
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.InheritsFromOrEquals(httpMethodBaseAttributeSymbol) == true) { httpAttr = attr; break; }
        }

        if (httpAttr != null && httpAttr.ConstructorArguments is { Length: >= 1 } args && args[0].Value is string path)
        {
            semantics.HttpMethod = ExtractHttpMethodName(httpAttr.AttributeClass!);
            semantics.PathFragments = ParsePathFragments(path, methodSymbol).ToImmutableEquatableArray();
        }

        // MultipartAttribute
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "MultipartAttribute" && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI")
            {
                semantics.IsMultipart = true;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string boundary)
                    semantics.MultipartBoundary = boundary;
                break;
            }
        }

        // Headers (interface + method level)
        var headersList = new List<string>();
        foreach (var attr in methodSymbol.ContainingType.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "HeadersAttribute" && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs)
            {
                foreach (var h in cargs[0].Values)
                    if (h.Value is string hs) headersList.Add(hs);
            }
        }
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "HeadersAttribute" && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs)
            {
                foreach (var h in cargs[0].Values)
                    if (h.Value is string hs) headersList.Add(hs);
                break;
            }
        }
        semantics.Headers = headersList.ToImmutableEquatableArray();

        // QueryUriFormatAttribute
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "QueryUriFormatAttribute" && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is int uriFormat)
            {
                semantics.QueryUriFormat = uriFormat;
                break;
            }
        }

        // Classify parameters
        var pathParamIndices = new HashSet<int>();
        foreach (var frag in semantics.PathFragments)
            if (!frag.IsConstant) pathParamIndices.Add(frag.ParameterIndex);

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var param = methodSymbol.Parameters[i];
            var classification = ClassifyParameter(param, i);

            // If parameter is in path and not explicitly classified, mark as Path
            if (classification.Kind == ParameterKind.None && pathParamIndices.Contains(i))
                classification.Kind = ParameterKind.Path;

            // If multipart and not classified and not in path, mark as Multipart
            if (semantics.IsMultipart && classification.Kind == ParameterKind.None && !pathParamIndices.Contains(i))
                classification.Kind = ParameterKind.Multipart;

            // If not multipart, not classified, not in path, not cancellation token → default to Query
            if (!semantics.IsMultipart && classification.Kind == ParameterKind.None && !pathParamIndices.Contains(i))
                classification.Kind = ParameterKind.Query;

            semantics.ParameterClassifications.Add(classification);

            if (classification.Kind == ParameterKind.Body)
            {
                semantics.BodyParameterIndex = i;
                semantics.BodySerializationMethod = (int)classification.BodySerializationMethod;
                semantics.BodyBuffered = classification.BodyBuffered;
            }
            if (classification.Kind == ParameterKind.CancellationToken)
                semantics.CancellationTokenIndex = i;
        }

        return semantics;
    }

    static string ExtractHttpMethodName(INamedTypeSymbol attrClass) => attrClass.Name switch
    {
        "GetAttribute" => "GET",
        "PostAttribute" => "POST",
        "PutAttribute" => "PUT",
        "DeleteAttribute" => "DELETE",
        "PatchAttribute" => "PATCH",
        "OptionsAttribute" => "OPTIONS",
        "HeadAttribute" => "HEAD",
        _ => "GET",
    };

    static List<PathFragmentModel> ParsePathFragments(string path, IMethodSymbol methodSymbol)
    {
        var fragments = new List<PathFragmentModel>();
        var paramNames = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
            paramNames[methodSymbol.Parameters[i].Name] = i;

        var sb = new StringBuilder();
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '{')
            {
                if (sb.Length > 0) { fragments.Add(PathFragmentModel.Constant(sb.ToString())); sb.Clear(); }
                var close = path.IndexOf('}', i + 1);
                if (close < 0) { sb.Append(path.Substring(i)); break; }
                var name = path.Substring(i + 1, close - i - 1);
                if (paramNames.TryGetValue(name, out var idx))
                    fragments.Add(PathFragmentModel.Parameter(idx));
                else
                    sb.Append('{').Append(name).Append('}');
                i = close;
            }
            else
            {
                sb.Append(path[i]);
            }
        }
        if (sb.Length > 0) fragments.Add(PathFragmentModel.Constant(sb.ToString()));
        return fragments;
    }

    static ParameterModel ParseParameterWithKind(IParameterSymbol param, int paramIndex, HttpSemantics httpSemantics)
    {
        var baseParam = ParseParameter(param);
        var classification = httpSemantics.ParameterClassifications.FirstOrDefault(c => c.Index == paramIndex);

        return baseParam with
        {
            Kind = classification.Kind,
            AliasAs = classification.AliasAs,
            HeaderName = classification.HeaderName,
            AuthorizeScheme = classification.AuthorizeScheme,
            PropertyKey = classification.PropertyKey,
            QueryFormat = classification.QueryFormat,
            QueryPrefix = classification.QueryPrefix,
            QueryDelimiter = classification.QueryDelimiter,
            QueryTreatAsString = classification.QueryTreatAsString,
            QueryCollectionFormat = classification.QueryCollectionFormat,
            QueryIsCollectionFormatSpecified = classification.QueryIsCollectionFormatSpecified,
        };
    }

    static ParameterClassification ClassifyParameter(IParameterSymbol param, int index)
    {
        var result = new ParameterClassification { Index = index };

        if (IsCancellationTokenParameter(param))
        {
            result.Kind = ParameterKind.CancellationToken;
            return result;
        }

        foreach (var attr in param.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            var attrNs = attr.AttributeClass?.ContainingNamespace?.ToDisplayString();

            if (attrName == "BodyAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.Body;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs)
                {
                    if (cargs[0].Value is int serMethod) result.BodySerializationMethod = (BodySerializationMethod)serMethod;
                    else if (cargs[0].Value is bool buffered) result.BodyBuffered = buffered;
                }
                if (attr.ConstructorArguments is { Length: >= 2 } cargs2 && cargs2[1].Value is bool buffered2)
                    result.BodyBuffered = buffered2;
                return result;
            }

            if (attrName == "HeaderAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.Header;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string header)
                    result.HeaderName = header;
                return result;
            }

            if (attrName == "HeaderCollectionAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.HeaderCollection;
                return result;
            }

            if (attrName == "AuthorizeAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.Authorize;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string scheme)
                    result.AuthorizeScheme = scheme;
                return result;
            }

            if (attrName == "PropertyAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.Property;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string key)
                    result.PropertyKey = key;
                return result;
            }

            if (attrName == "AliasAsAttribute" && attrNs == "Observables.RestAPI")
            {
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string alias)
                    result.AliasAs = alias;
            }

            if (attrName == "QueryAttribute" && attrNs == "Observables.RestAPI")
            {
                result.Kind = ParameterKind.Query;
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Format" && namedArg.Value.Value is string format) result.QueryFormat = format;
                    if (namedArg.Key == "Prefix" && namedArg.Value.Value is string prefix) result.QueryPrefix = prefix;
                    if (namedArg.Key == "TreatAsString" && namedArg.Value.Value is bool treatAsString) result.QueryTreatAsString = treatAsString;
                    if (namedArg.Key == "CollectionFormat" && namedArg.Value.Value is int cf)
                    {
                        result.QueryCollectionFormat = cf;
                        result.QueryIsCollectionFormatSpecified = true;
                    }
                }
                if (attr.ConstructorArguments is { Length: >= 1 } cargs)
                {
                    if (cargs[0].Value is string delimiter) result.QueryDelimiter = delimiter;
                    else if (cargs[0].Value is int cf)
                    {
                        result.QueryCollectionFormat = cf;
                        result.QueryIsCollectionFormatSpecified = true;
                    }
                }
                if (attr.ConstructorArguments is { Length: >= 2 } cargs2 && cargs2[1].Value is string prefix2)
                    result.QueryPrefix = prefix2;
                if (attr.ConstructorArguments is { Length: >= 3 } cargs3 && cargs3[2].Value is string format2)
                    result.QueryFormat = format2;
                return result;
            }
        }

        result.Kind = ParameterKind.None;
        return result;
    }
}
