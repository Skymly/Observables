using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Observables.RestAPI;

namespace Observables.RestAPI.Generators;

internal static partial class Parser
{
    internal readonly struct ParsedHttpMethod
    {
        public ParsedHttpMethod(
            ImmutableEquatableArray<ParameterModel> parameters,
            RestApiMethodSpecModel spec,
            int? cancellationTokenIndex)
        {
            Parameters = parameters;
            Spec = spec;
            CancellationTokenIndex = cancellationTokenIndex;
        }

        public ImmutableEquatableArray<ParameterModel> Parameters { get; }
        public RestApiMethodSpecModel Spec { get; }
        public int? CancellationTokenIndex { get; }
    }

    sealed class ParameterClassification
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
        public bool QueryCollectionFormatSpecified { get; set; }
        public BodySerializationMethod BodySerializationMethod { get; set; } = BodySerializationMethod.Default;
        public bool? BodyBuffered { get; set; }
    }

    static ParsedHttpMethod ParseHttpMethod(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol httpMethodBaseAttributeSymbol,
        List<Diagnostic> diagnostics)
    {
        string httpMethod = "";
        string rawPath = "";
        var isMultipart = false;
        var multipartBoundary = "----MyGreatBoundary";
        var queryUriFormat = (int)UriFormat.UriEscaped;
        var headersList = new List<string>();

        AttributeData? httpAttr = null;
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.InheritsFromOrEquals(httpMethodBaseAttributeSymbol) == true)
            {
                httpAttr = attr;
                break;
            }
        }

        if (httpAttr != null && httpAttr.ConstructorArguments is { Length: >= 1 } args && args[0].Value is string path)
        {
            httpMethod = ExtractHttpMethodName(httpAttr.AttributeClass!);
            rawPath = path;
        }

        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "MultipartAttribute"
                && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI")
            {
                isMultipart = true;
                if (attr.ConstructorArguments is { Length: >= 1 } cargs && cargs[0].Value is string boundary)
                    multipartBoundary = boundary;
                break;
            }
        }

        foreach (var attr in methodSymbol.ContainingType.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "HeadersAttribute"
                && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs)
            {
                foreach (var h in cargs[0].Values)
                    if (h.Value is string hs)
                        headersList.Add(hs);
            }
        }

        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "HeadersAttribute"
                && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs)
            {
                foreach (var h in cargs[0].Values)
                    if (h.Value is string hs)
                        headersList.Add(hs);
                break;
            }
        }

        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "QueryUriFormatAttribute"
                && attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } cargs
                && cargs[0].Value is int uriFormat)
            {
                queryUriFormat = uriFormat;
                break;
            }
        }

        var classifications = new List<ParameterClassification>(methodSymbol.Parameters.Length);
        var slots = new List<RestApiParameterSlot>(methodSymbol.Parameters.Length);
        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var classification = ClassifyParameter(methodSymbol.Parameters[i], i);
            classifications.Add(classification);
            slots.Add(new RestApiParameterSlot(
                classification.AliasAs ?? methodSymbol.Parameters[i].Name,
                ToDeclaredKind(classification.Kind)));
        }

        var template = RestApiPathTemplate.Parse(rawPath);
        var occupancy = template.Occupy(slots);
        if (!string.IsNullOrEmpty(rawPath) && !occupancy.Matches)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.PathParameterMismatch,
                methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name));
        }

        int? cancellationTokenIndex = null;
        var bodySerializationMethod = 0;
        bool? bodyBuffered = null;
        var bindings = new List<RestApiBindingModel>();
        var argIndex = 0;

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var classification = classifications[i];
            var param = methodSymbol.Parameters[i];

            if (classification.Kind == ParameterKind.None
                && occupancy.PathKindNames.Contains(classification.AliasAs ?? param.Name))
                classification.Kind = ParameterKind.Path;
            else if (isMultipart && classification.Kind == ParameterKind.None)
                classification.Kind = ParameterKind.Multipart;
            else if (classification.Kind == ParameterKind.None)
                classification.Kind = ParameterKind.Query;

            if (classification.Kind == ParameterKind.Body)
            {
                bodySerializationMethod = (int)classification.BodySerializationMethod;
                bodyBuffered = classification.BodyBuffered;
            }

            if (classification.Kind == ParameterKind.CancellationToken)
            {
                cancellationTokenIndex = i;
                continue;
            }

            bindings.Add(ToBinding(classification, argIndex, param.MetadataName));
            argIndex++;
        }

        var parameters = methodSymbol.Parameters
            .Select((p, i) => ParseParameter(p) with { Kind = classifications[i].Kind, AliasAs = classifications[i].AliasAs, HeaderName = classifications[i].HeaderName, AuthorizeScheme = classifications[i].AuthorizeScheme, PropertyKey = classifications[i].PropertyKey, QueryFormat = classifications[i].QueryFormat, QueryPrefix = classifications[i].QueryPrefix, QueryDelimiter = classifications[i].QueryDelimiter, QueryTreatAsString = classifications[i].QueryTreatAsString, QueryCollectionFormat = classifications[i].QueryCollectionFormat, QueryIsCollectionFormatSpecified = classifications[i].QueryCollectionFormatSpecified })
            .ToImmutableEquatableArray();

        var spec = new RestApiMethodSpecModel(
            httpMethod,
            rawPath,
            bindings.ToImmutableEquatableArray(),
            IsMultipart: isMultipart,
            MultipartBoundary: multipartBoundary,
            QueryUriFormat: queryUriFormat,
            BodySerializationMethod: bodySerializationMethod,
            BodyBuffered: bodyBuffered,
            StaticHeaders: headersList.ToImmutableEquatableArray());

        return new ParsedHttpMethod(parameters, spec, cancellationTokenIndex);
    }

    static RestApiBindingModel ToBinding(ParameterClassification classification, int argIndex, string metadataName)
    {
        var kind = ToSlotKind(classification.Kind);
        var name = classification.Kind switch
        {
            ParameterKind.Query => classification.AliasAs ?? metadataName,
            ParameterKind.Path => classification.AliasAs ?? metadataName,
            ParameterKind.Header => classification.HeaderName ?? metadataName,
            ParameterKind.Property => classification.PropertyKey ?? metadataName,
            _ => metadataName,
        };

        return new RestApiBindingModel(
            kind,
            argIndex,
            name,
            HeaderName: classification.HeaderName,
            AuthorizeScheme: classification.AuthorizeScheme,
            PropertyKey: classification.PropertyKey,
            QueryFormat: classification.QueryFormat,
            QueryPrefix: classification.QueryPrefix,
            QueryDelimiter: classification.QueryDelimiter,
            QueryTreatAsString: classification.QueryTreatAsString,
            QueryCollectionFormat: classification.QueryCollectionFormat,
            QueryIsCollectionFormatSpecified: classification.QueryCollectionFormatSpecified,
            NameIsExplicit: classification.Kind == ParameterKind.Query && classification.AliasAs != null);
    }

    static RestApiDeclaredKind ToDeclaredKind(ParameterKind kind) => kind switch
    {
        ParameterKind.Path => RestApiDeclaredKind.Path,
        ParameterKind.Query => RestApiDeclaredKind.Query,
        ParameterKind.Body => RestApiDeclaredKind.Body,
        ParameterKind.Header => RestApiDeclaredKind.Header,
        ParameterKind.HeaderCollection => RestApiDeclaredKind.HeaderCollection,
        ParameterKind.Authorize => RestApiDeclaredKind.Authorize,
        ParameterKind.Property => RestApiDeclaredKind.Property,
        ParameterKind.Multipart => RestApiDeclaredKind.Multipart,
        ParameterKind.CancellationToken => RestApiDeclaredKind.Cancellation,
        _ => RestApiDeclaredKind.None,
    };

    static RestApiSlotKind ToSlotKind(ParameterKind kind) => kind switch
    {
        ParameterKind.Path => RestApiSlotKind.Path,
        ParameterKind.Query => RestApiSlotKind.Query,
        ParameterKind.Header => RestApiSlotKind.Header,
        ParameterKind.HeaderCollection => RestApiSlotKind.HeaderCollection,
        ParameterKind.Authorize => RestApiSlotKind.Authorize,
        ParameterKind.Property => RestApiSlotKind.Property,
        ParameterKind.Body => RestApiSlotKind.Body,
        ParameterKind.Multipart => RestApiSlotKind.Multipart,
        _ => RestApiSlotKind.Query,
    };

    static string ExtractHttpMethodName(INamedTypeSymbol attrClass)
    {
        for (var type = attrClass; type is not null; type = type.BaseType)
        {
            switch (type.Name)
            {
                case "GetAttribute":
                    return "GET";
                case "PostAttribute":
                    return "POST";
                case "PutAttribute":
                    return "PUT";
                case "DeleteAttribute":
                    return "DELETE";
                case "PatchAttribute":
                    return "PATCH";
                case "OptionsAttribute":
                    return "OPTIONS";
                case "HeadAttribute":
                    return "HEAD";
            }
        }

        var name = attrClass.Name;
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "Attribute".Length);

        return name.Length == 0 ? "GET" : name.ToUpperInvariant();
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
            var aliasName = attr.AttributeClass?.Name;
            var aliasNs = attr.AttributeClass?.ContainingNamespace?.ToDisplayString();
            if (aliasName == "AliasAsAttribute" && aliasNs == "Observables.RestAPI"
                && attr.ConstructorArguments is { Length: >= 1 } aliasArgs
                && aliasArgs[0].Value is string alias)
            {
                result.AliasAs = alias;
            }
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
                        result.QueryCollectionFormatSpecified = true;
                    }
                }
                if (attr.ConstructorArguments is { Length: >= 1 } cargs)
                {
                    if (cargs[0].Value is string delimiter) result.QueryDelimiter = delimiter;
                    else if (cargs[0].Value is int cf)
                    {
                        result.QueryCollectionFormat = cf;
                        result.QueryCollectionFormatSpecified = true;
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

