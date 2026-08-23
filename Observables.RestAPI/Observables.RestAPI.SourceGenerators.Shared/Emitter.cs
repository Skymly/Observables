using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Observables.RestAPI.Generators;

internal static class Emitter
{
    public static void EmitSharedCode(
        ContextGenerationModel model,
        Action<string, SourceText> addSource
    )
    {
        if (model.Interfaces.Count == 0)
            return;

        // No PreserveAttribute generation — Path B eliminates it.
        // Only emit the ModuleInitializer that registers generated factories.
        var generatedFactoryRegistrations = string.Join(
            "\n",
            model.Interfaces
                .Where(static interfaceModel => !interfaceModel.ClassDeclaration.Contains("<"))
                .Select(static interfaceModel =>
                    $"                        global::Observables.RestAPI.RestService.RegisterGeneratedFactory(typeof({interfaceModel.InterfaceDisplayName}), static (client, settings) => new global::Observables.RestAPI.Implementation.Generated.{interfaceModel.Ns}{interfaceModel.ClassSuffix}(client, settings));"
                )
        );

        addSource(
            "Generated.g.cs",
            GeneratedSourceHeader.ToSourceText(
                $$"""
                namespace Observables.RestAPI.Implementation
                {

                    /// <inheritdoc />
                    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                    [global::System.Diagnostics.DebuggerNonUserCode]
                    [global::System.Reflection.Obfuscation(Exclude=true)]
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static partial class Generated
                    {
                #if NET5_0_OR_GREATER
                        [System.Runtime.CompilerServices.ModuleInitializer]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ILLink", "IL2026", Justification = "Factory registration only; the proxy is invoked by user code that declares RequiresUnreferencedCode.")]
                        public static void Initialize()
                        {
                {{generatedFactoryRegistrations}}
                        }
                #endif
                    }
                }
                """));
    }

    public static SourceText EmitInterface(InterfaceModel model)
    {
        var source = new SourceWriter();
        GeneratedSourceHeader.WritePrefix(source, model.Nullability);

        source.WriteLine(
            $$"""
            namespace Observables.RestAPI.Implementation
            {

                partial class Generated
                {

                /// <inheritdoc />
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                [global::System.Diagnostics.DebuggerNonUserCode]
                [global::System.Reflection.Obfuscation(Exclude=true)]
                [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
            #if NET8_0_OR_GREATER
                [global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("RestAPI uses reflection on interface methods and DTO types. Preserve required members when trimming.")]
                [global::System.Diagnostics.CodeAnalysis.RequiresDynamicCode("RestAPI uses MakeGenericMethod and reflection at runtime.")]
            #endif
                partial class {{model.Ns}}{{model.ClassDeclaration}}
                    : {{model.InterfaceDisplayName}}
            """
        );

        source.Indentation += 2;
        GenerateConstraints(source, model.Constraints, false);
        source.Indentation--;

        source.WriteLine(
            $$"""
            {
                /// <inheritdoc />
                public global::System.Net.Http.HttpClient Client { get; }
                readonly global::Observables.RestAPI.RestApiSettings _settings;

                /// <inheritdoc />
                public {{model.Ns}}{{model.ClassSuffix}}(global::System.Net.Http.HttpClient client, global::Observables.RestAPI.RestApiSettings? settings)
                {
                    Client = client;
                    _settings = settings ?? new global::Observables.RestAPI.RestApiSettings();
                }

            """
        );

        source.Indentation++;

        foreach (var method in model.HttpMethods)
            WriteHttpMethod(source, method, true);

        foreach (var method in model.DerivedHttpMethods)
            WriteHttpMethod(source, method, false);

        foreach (var method in model.NonHttpMethods)
            WriteNonHttpMethod(source, method);

        if (model.DisposeMethod)
            WriteDisposableMethod(source);

        source.Indentation -= 2;
        source.WriteLine(
            """
                }
                }
            }

            #pragma warning restore
            """
        );
        return source.ToSourceText();
    }

    /// <summary>
    /// Generates the body of a REST method that directly builds and sends an HttpRequestMessage.
    /// </summary>
    static void WriteHttpMethod(
        SourceWriter source,
        MethodModel methodModel,
        bool isTopLevel
    )
    {
        var (isAsync, @return, configureAwait) = methodModel.ReturnTypeMetadata switch
        {
            ReturnTypeInfo.AsyncVoid => (true, "await ", ".ConfigureAwait(false)"),
            ReturnTypeInfo.AsyncResult => (true, "return await ", ".ConfigureAwait(false)"),
            ReturnTypeInfo.Return => (false, "return ", ""),
            ReturnTypeInfo.R3Observable => (false, "return ", ""),
            ReturnTypeInfo.SystemReactiveObservable => (false, "return ", ""),
            ReturnTypeInfo.SyncVoid => (false, "", ""),
            ReturnTypeInfo.Unsupported => throw new ArgumentOutOfRangeException(nameof(methodModel.ReturnTypeMetadata), methodModel.ReturnTypeMetadata, "Unsupported return type."),
            _ => throw new ArgumentOutOfRangeException(nameof(methodModel.ReturnTypeMetadata), methodModel.ReturnTypeMetadata, "Unsupported value."),
        };

        var isExplicit = methodModel.IsExplicitInterface || !isTopLevel;
        WriteMethodOpening(source, methodModel, isExplicit, isExplicit, isAsync);

        // Generate the request body
        var needsAsyncWrapper = methodModel.ReturnTypeMetadata is ReturnTypeInfo.R3Observable or ReturnTypeInfo.SystemReactiveObservable;
        if (needsAsyncWrapper)
        {
            WriteObservableBody(source, methodModel);
        }
        else
        {
            WriteDirectBody(source, methodModel, @return, configureAwait);
        }

        WriteMethodClosing(source);
    }

    static void WriteDirectBody(SourceWriter source, MethodModel methodModel, string @return, string configureAwait)
    {
        var ctVar = "______ct";
        var ctParamIndex = methodModel.CancellationTokenIndex;

        // Extract cancellation token
        if (ctParamIndex.HasValue)
        {
            source.WriteLine($"var {ctVar} = @{methodModel.Parameters[ctParamIndex.Value].MetadataName};");
        }
        else
        {
            source.WriteLine($"var {ctVar} = global::System.Threading.CancellationToken.None;");
        }

        WriteRequestBuilding(source, methodModel);

        var bodyBufferedExpression = GetBodyBufferedExpression(methodModel);

        // Send and handle response
        if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.AsyncVoid)
        {
            source.WriteLine($"await global::Observables.RestAPI.RestApiBridge.SendVoidAsync(Client, ______request, _settings, {ctVar}){configureAwait};");
        }
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.SyncVoid)
        {
            source.WriteLine($"global::Observables.RestAPI.RestApiBridge.SendVoidAsync(Client, ______request, _settings, {ctVar}).GetAwaiter().GetResult();");
        }
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.AsyncResult)
        {
            source.WriteLine($"{@return}global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, ______request, _settings, {bodyBufferedExpression}, {ctVar}){configureAwait};");
        }
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.Return)
        {
            // Synchronous return — block on the async call
            source.WriteLine($"{@return}global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, ______request, _settings, {bodyBufferedExpression}, {ctVar}).GetAwaiter().GetResult();");
        }
    }

    static void WriteObservableBody(SourceWriter source, MethodModel methodModel)
    {
#if RESTAPI_R3
        source.WriteLine($"return global::R3.Observable.FromAsync(async ______ct =>");
#elif RESTAPI_REACTIVE
        source.WriteLine($"return global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter.FromAsync(async ______ct =>");
#else
        source.WriteLine($"return global::R3.Observable.FromAsync(async ______ct =>");
#endif
        source.WriteLine("{");
        source.Indentation++;

        WriteRequestBuilding(source, methodModel);

        // Send
        source.WriteLine($"return await global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, ______request, _settings, {GetBodyBufferedExpression(methodModel)}, ______ct).ConfigureAwait(false);");

        source.Indentation--;
        source.WriteLine("});");
    }

    static string GetBodyBufferedExpression(MethodModel methodModel) =>
        methodModel.BodyBuffered.HasValue
            ? (methodModel.BodyBuffered.Value ? "true" : "false")
            : (methodModel.BodyParameterIndex.HasValue || methodModel.IsMultipart ? "_settings.Buffered" : "false");

    /// <summary>
    /// Emits the common request-building statements shared by direct and observable method bodies:
    /// BaseAddress check, path construction, query parameters, request message creation,
    /// multipart setup, headers, parameter processing, and RequestUri assignment.
    /// </summary>
    static void WriteRequestBuilding(SourceWriter source, MethodModel methodModel)
    {
        // BaseAddress check
        source.WriteLine("""if (Client.BaseAddress == null) throw new global::System.InvalidOperationException("BaseAddress must be set on the HttpClient instance");""");

        // Build path
        source.WriteLine("var ______path = " + BuildPathExpression(methodModel) + ";");

        // Build query params
        WriteQueryParameters(source, methodModel);

        // Create request
        source.WriteLine($"var ______request = new global::System.Net.Http.HttpRequestMessage {{ Method = {GetHttpMethodExpression(methodModel.HttpMethod)} }};");

        // Multipart content
        if (methodModel.IsMultipart)
        {
            source.WriteLine($"var ______multipart = new global::System.Net.Http.MultipartFormDataContent(\"{EscapeString(methodModel.MultipartBoundary)}\");");
            source.WriteLine("______request.Content = ______multipart;");
        }

        // Add headers
        WriteHeaders(source, methodModel.Headers);

        // Process parameters: headers, authorize, property, body, multipart
        WriteParameters(source, methodModel);

        // Set RequestUri
        source.WriteLine("______request.RequestUri = new global::System.Uri(______path, global::System.UriKind.Relative);");
    }

    static void WriteQueryParameters(SourceWriter source, MethodModel methodModel)
    {
        var hasQuery = methodModel.Parameters.Any(p => p.Kind == ParameterKind.Query);
        if (!hasQuery)
            return;

        source.WriteLine("var ______queryParams = new global::System.Collections.Generic.List<global::System.Collections.Generic.KeyValuePair<string, string?>>();");
        foreach (var param in methodModel.Parameters)
        {
            if (param.Kind == ParameterKind.Query)
            {
                var key = param.AliasAs ?? param.MetadataName;
                var prefix = EscapeString(param.QueryPrefix ?? "");
                var delimiter = EscapeString(param.QueryDelimiter);
                var format = EscapeString(param.QueryFormat ?? "");
                source.WriteLine($"global::Observables.RestAPI.RestApiBridge.AddQueryParameter(______queryParams, \"{EscapeString(key)}\", @{param.MetadataName}, _settings, prefix: \"{prefix}\", delimiter: \"{delimiter}\", format: \"{format}\", treatAsString: {(param.QueryTreatAsString ? "true" : "false")}, collectionFormat: {param.QueryCollectionFormat}, isCollectionFormatSpecified: {(param.QueryIsCollectionFormatSpecified ? "true" : "false")});");
            }
        }
        source.WriteLine($"______path = global::Observables.RestAPI.RestApiBridge.BuildRelativePath(______path, ______queryParams, (global::System.UriFormat){methodModel.QueryUriFormat});");
    }

    static void WriteHeaders(SourceWriter source, ImmutableEquatableArray<string> headers)
    {
        foreach (var header in headers)
        {
            var colonIdx = header.IndexOf(':');
            if (colonIdx > 0)
            {
                var hKey = header.Substring(0, colonIdx).Trim();
                var hVal = colonIdx + 1 < header.Length ? header.Substring(colonIdx + 1).Trim() : "";
                source.WriteLine($"______request.Headers.TryAddWithoutValidation(\"{EscapeString(hKey)}\", \"{EscapeString(hVal)}\");");
            }
        }
    }

    static void WriteParameters(SourceWriter source, MethodModel methodModel)
    {
        foreach (var param in methodModel.Parameters)
        {
            switch (param.Kind)
            {
                case ParameterKind.Header:
                    var headerName = param.HeaderName ?? param.MetadataName;
                    source.WriteLine($"______request.Headers.TryAddWithoutValidation(\"{EscapeString(headerName)}\", global::Observables.RestAPI.RestApiBridge.FormatQueryValue(@{param.MetadataName}, _settings));");
                    break;
                case ParameterKind.HeaderCollection:
                    source.WriteLine($"if (@{param.MetadataName} != null) foreach (var ______hdr in @{param.MetadataName}) ______request.Headers.TryAddWithoutValidation(______hdr.Key, ______hdr.Value);");
                    break;
                case ParameterKind.Authorize:
                    var scheme = param.AuthorizeScheme ?? "Bearer";
                    source.WriteLine($"______request.Headers.TryAddWithoutValidation(\"Authorization\", \"{scheme} \" + @{param.MetadataName});");
                    break;
                case ParameterKind.Property:
                    var propKey = param.PropertyKey ?? param.MetadataName;
                    source.WriteLine("#if NET6_0_OR_GREATER");
                    source.WriteLine($"______request.Options.Set(new global::System.Net.Http.HttpRequestOptionsKey<object>(\"{EscapeString(propKey)}\"), @{param.MetadataName}!);");
                    source.WriteLine("#else");
                    source.WriteLine($"______request.Properties[\"{EscapeString(propKey)}\"] = @{param.MetadataName}!;");
                    source.WriteLine("#endif");
                    break;
                case ParameterKind.Body:
                    WriteBodyContent(source, methodModel, param);
                    break;
                case ParameterKind.Multipart:
                    source.WriteLine($"global::Observables.RestAPI.RestApiBridge.AddMultipartItem(______multipart, \"{EscapeString(param.MetadataName)}\", \"{EscapeString(param.MetadataName)}\", @{param.MetadataName}, _settings);");
                    break;
            }
        }
    }

    static void WriteBodyContent(SourceWriter source, MethodModel methodModel, ParameterModel param)
    {
        var bodySerMethod = (BodySerializationMethod)methodModel.BodySerializationMethod;

        if (bodySerMethod == BodySerializationMethod.UrlEncoded)
        {
            source.WriteLine($"______request.Content = global::Observables.RestAPI.RestApiBridge.CreateFormUrlEncodedContent(@{param.MetadataName}!, _settings);");
        }
        else
        {
            source.WriteLine($"______request.Content = global::Observables.RestAPI.RestApiBridge.SerializeBody(@{param.MetadataName}!, _settings, {methodModel.BodySerializationMethod});");
        }
    }

    static string BuildPathExpression(MethodModel methodModel)
    {
        if (methodModel.PathFragments.Count == 0)
            return "\"\"";

        var parts = new List<string>();
        foreach (var frag in methodModel.PathFragments)
        {
            if (frag.IsConstant)
            {
                parts.Add($"\"{EscapeString(frag.ConstantValue!)}\"");
            }
            else
            {
                var paramName = methodModel.Parameters[frag.ParameterIndex].MetadataName;
                parts.Add($"global::Observables.RestAPI.RestApiBridge.FormatPathParameter(@{paramName}, _settings)");
            }
        }

        if (parts.Count == 1) return parts[0];
        return string.Join(" + ", parts);
    }

    static string GetHttpMethodExpression(string httpMethod) => httpMethod switch
    {
        "GET" => "global::System.Net.Http.HttpMethod.Get",
        "POST" => "global::System.Net.Http.HttpMethod.Post",
        "PUT" => "global::System.Net.Http.HttpMethod.Put",
        "DELETE" => "global::System.Net.Http.HttpMethod.Delete",
        "HEAD" => "global::System.Net.Http.HttpMethod.Head",
        "PATCH" => "new global::System.Net.Http.HttpMethod(\"PATCH\")",
        "OPTIONS" => "new global::System.Net.Http.HttpMethod(\"OPTIONS\")",
        _ => "global::System.Net.Http.HttpMethod.Get",
    };

    static string EscapeString(string s) => s
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t");

    static void WriteNonHttpMethod(SourceWriter source, MethodModel methodModel)
    {
        var isExplicit = methodModel.IsExplicitInterface;
        WriteMethodOpening(source, methodModel, isExplicit, isExplicit);
        source.WriteLine(
            @"throw new global::System.NotImplementedException(""Either this method has no Rest API HTTP method attribute or you've used something other than a string literal for the 'path' argument."");"
        );
        WriteMethodClosing(source);
    }

    static void WriteDisposableMethod(SourceWriter source)
    {
        source.WriteLine(
            """
            /// <inheritdoc />
            void global::System.IDisposable.Dispose()
            {
                    Client?.Dispose();
            }
            """
        );
    }

    static void WriteMethodOpening(
        SourceWriter source,
        MethodModel methodModel,
        bool isDerivedExplicitImpl,
        bool isExplicitInterface,
        bool isAsync = false
    )
    {
        var visibility = !isExplicitInterface ? "public " : string.Empty;
        var async = isAsync ? "async " : "";

        var builder = new StringBuilder();
        builder.Append(
            @$"/// <inheritdoc />
{visibility}{async}{methodModel.ReturnType} "
        );

        if (isExplicitInterface)
        {
            var ct = methodModel.ContainingType;
            if (!ct.StartsWith("global::"))
                ct = "global::" + ct;
            builder.Append(@$"{ct}.");
        }
        builder.Append(@$"{methodModel.DeclaredMethod}(");

        if (methodModel.Parameters.Count > 0)
        {
            var list = new List<string>();
            foreach (var param in methodModel.Parameters)
            {
                var annotation = param.Annotation;
                list.Add($@"{param.Type}{(annotation ? '?' : string.Empty)} @{param.MetadataName}");
            }
            builder.Append(string.Join(", ", list));
        }

        builder.Append(")");

        source.WriteLine();
        source.WriteLine(builder.ToString());
        source.Indentation++;
        GenerateConstraints(source, methodModel.Constraints, isDerivedExplicitImpl || isExplicitInterface);
        source.Indentation--;
        source.WriteLine("{");
        source.Indentation++;
    }

    static void WriteMethodClosing(SourceWriter source)
    {
        source.Indentation--;
        source.WriteLine("}");
    }

    static void GenerateConstraints(
        SourceWriter writer,
        ImmutableEquatableArray<TypeConstraint> typeParameters,
        bool isOverrideOrExplicitImplementation
    )
    {
        foreach (var typeParameter in typeParameters)
            WriteConstraintsForTypeParameter(writer, typeParameter, isOverrideOrExplicitImplementation);
    }

    static void WriteConstraintsForTypeParameter(
        SourceWriter source,
        TypeConstraint typeParameter,
        bool isOverrideOrExplicitImplementation
    )
    {
        var parameters = new List<string>();
        var knownConstraints = typeParameter.KnownTypeConstraint;
        if (knownConstraints.HasFlag(KnownTypeConstraint.Class)) parameters.Add("class");
        if (knownConstraints.HasFlag(KnownTypeConstraint.Unmanaged) && !isOverrideOrExplicitImplementation) parameters.Add("unmanaged");
        if (knownConstraints.HasFlag(KnownTypeConstraint.Struct)) parameters.Add("struct");
        if (knownConstraints.HasFlag(KnownTypeConstraint.NotNull) && !isOverrideOrExplicitImplementation) parameters.Add("notnull");
        if (!isOverrideOrExplicitImplementation) parameters.AddRange(typeParameter.Constraints);
        if (knownConstraints.HasFlag(KnownTypeConstraint.New) && !isOverrideOrExplicitImplementation) parameters.Add("new()");

        if (parameters.Count > 0)
            source.WriteLine($"where {typeParameter.TypeName} : {string.Join(", ", parameters)}");
    }
}
