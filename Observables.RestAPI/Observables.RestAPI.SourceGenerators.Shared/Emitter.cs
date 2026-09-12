using System.Collections.Generic;
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
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ILLink", "IL2026", Justification = "Factory registration only; the proxy is invoked by user code that declares RequiresUnreferencedCode.")]
                #endif
                        [global::System.Runtime.CompilerServices.ModuleInitializer]
                        public static void Initialize()
                        {
                {{generatedFactoryRegistrations}}
                        }
                    }
                }

                #if !NET5_0_OR_GREATER
                namespace System.Runtime.CompilerServices
                {
                    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
                    internal sealed partial class ModuleInitializerAttribute : global::System.Attribute
                    {
                    }
                }
                #endif
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

        var specIndex = 0;
        foreach (var method in model.HttpMethods)
            WriteSpecField(source, method, specIndex++);
        foreach (var method in model.DerivedHttpMethods)
            WriteSpecField(source, method, specIndex++);

        specIndex = 0;
        foreach (var method in model.HttpMethods)
            WriteHttpMethod(source, method, true, specIndex++);
        foreach (var method in model.DerivedHttpMethods)
            WriteHttpMethod(source, method, false, specIndex++);

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


    static string SpecFieldName(int index) => "______spec" + index;

    static void WriteSpecField(SourceWriter source, MethodModel methodModel, int index)
    {
        if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.Unsupported)
            return;

        var spec = methodModel.Spec;
        source.WriteLine($"static readonly global::Observables.RestAPI.RestApiBridge.MethodSpec {SpecFieldName(index)} =");
        source.Indentation++;
        var flags = FormatFlags(spec);
        var bindings = FormatBindings(spec);
        if (flags == null)
            source.WriteLine($"new(\"{EscapeString(spec.HttpMethod)}\", \"{EscapeString(spec.PathTemplate)}\", {bindings});");
        else
            source.WriteLine($"new(\"{EscapeString(spec.HttpMethod)}\", \"{EscapeString(spec.PathTemplate)}\", {bindings}, {flags});");
        source.Indentation--;
        source.WriteLine();
    }

    static string FormatBindings(RestApiMethodSpecModel spec)
    {
        if (spec.Bindings.Count == 0)
            return "null";

        var items = new List<string>();
        foreach (var binding in spec.Bindings)
        {
            var kind = $"global::Observables.RestAPI.RestApiBridge.SlotKind.{binding.Kind}";
            var parts = new List<string>
            {
                kind,
                binding.ArgIndex.ToString(),
                $"\"{EscapeString(binding.Name)}\"",
            };
            if (binding.HeaderName != null)
                parts.Add($"headerName: \"{EscapeString(binding.HeaderName)}\"");
            if (binding.AuthorizeScheme != null)
                parts.Add($"authorizeScheme: \"{EscapeString(binding.AuthorizeScheme)}\"");
            if (binding.PropertyKey != null)
                parts.Add($"propertyKey: \"{EscapeString(binding.PropertyKey)}\"");
            var query = FormatQueryOptions(binding);
            if (query != null)
                parts.Add($"query: {query}");
            items.Add($"new({string.Join(", ", parts)})");
        }

        return "new global::Observables.RestAPI.RestApiBridge.Binding[] { " + string.Join(", ", items) + " }";
    }

    static string? FormatQueryOptions(RestApiBindingModel binding)
    {
        if (binding.Kind != RestApiSlotKind.Query)
            return null;

        var inits = new List<string>();
        if (binding.QueryFormat != null)
            inits.Add($"format: \"{EscapeString(binding.QueryFormat)}\"");
        if (binding.QueryPrefix != null)
            inits.Add($"prefix: \"{EscapeString(binding.QueryPrefix)}\"");
        if (!string.IsNullOrEmpty(binding.QueryDelimiter) && binding.QueryDelimiter != ".")
            inits.Add($"delimiter: \"{EscapeString(binding.QueryDelimiter)}\"");
        if (binding.QueryTreatAsString)
            inits.Add("treatAsString: true");
        if (binding.QueryIsCollectionFormatSpecified)
        {
            inits.Add($"collectionFormat: {binding.QueryCollectionFormat}");
            inits.Add("collectionFormatSpecified: true");
        }

        if (inits.Count == 0)
            return null;
        return "new global::Observables.RestAPI.RestApiBridge.QueryOptions(" + string.Join(", ", inits) + ")";
    }

    static string? FormatFlags(RestApiMethodSpecModel spec)
    {
        var inits = new List<string>();
        if (spec.IsMultipart)
        {
            inits.Add("isMultipart: true");
            if (!string.IsNullOrEmpty(spec.MultipartBoundary) && spec.MultipartBoundary != "----MyGreatBoundary")
                inits.Add($"multipartBoundary: \"{EscapeString(spec.MultipartBoundary)}\"");
        }
        if (spec.QueryUriFormat != 1)
            inits.Add($"queryUriFormat: {spec.QueryUriFormat}");
        if (spec.BodySerializationMethod != 0)
            inits.Add($"bodySerializationMethod: {spec.BodySerializationMethod}");
        if (spec.BodyBuffered == true)
            inits.Add("bodyBuffered: true");
        else if (spec.BodyBuffered == false)
            inits.Add("bodyBuffered: false");
        if (spec.StaticHeaders.Count > 0)
        {
            var headers = string.Join(", ", spec.StaticHeaders.Select(h => $"\"{EscapeString(h)}\""));
            inits.Add($"staticHeaders: new string[] {{ {headers} }}");
        }

        if (inits.Count == 0)
            return null;
        return "new global::Observables.RestAPI.RestApiBridge.MethodFlags(" + string.Join(", ", inits) + ")";
    }

    static void WriteHttpMethod(
        SourceWriter source,
        MethodModel methodModel,
        bool isTopLevel,
        int specIndex
    )
    {
        if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.Unsupported)
            return;

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

        var needsAsyncWrapper = methodModel.ReturnTypeMetadata is ReturnTypeInfo.R3Observable or ReturnTypeInfo.SystemReactiveObservable;
        if (needsAsyncWrapper)
            WriteObservableBody(source, methodModel, specIndex);
        else
            WriteDirectBody(source, methodModel, @return, configureAwait, specIndex);

        WriteMethodClosing(source);
    }

    static void WriteDirectBody(SourceWriter source, MethodModel methodModel, string @return, string configureAwait, int specIndex)
    {
        var ctVar = "______ct";
        var ctParamIndex = methodModel.CancellationTokenIndex;
        if (ctParamIndex.HasValue)
            source.WriteLine($"var {ctVar} = @{methodModel.Parameters[ctParamIndex.Value].MetadataName};");
        else
            source.WriteLine($"var {ctVar} = global::System.Threading.CancellationToken.None;");

        var args = FormatSendArgs(methodModel);
        var specField = SpecFieldName(specIndex);

        if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.AsyncVoid)
            source.WriteLine($"await global::Observables.RestAPI.RestApiBridge.SendVoidAsync(Client, _settings, in {specField}, {ctVar}{args}){configureAwait};");
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.SyncVoid)
            source.WriteLine($"global::Observables.RestAPI.RestApiBridge.SendVoidAsync(Client, _settings, in {specField}, {ctVar}{args}).GetAwaiter().GetResult();");
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.AsyncResult)
            source.WriteLine($"{@return}global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, _settings, in {specField}, {ctVar}{args}){configureAwait};");
        else if (methodModel.ReturnTypeMetadata == ReturnTypeInfo.Return)
            source.WriteLine($"{@return}global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, _settings, in {specField}, {ctVar}{args}).GetAwaiter().GetResult();");
    }

    static void WriteObservableBody(SourceWriter source, MethodModel methodModel, int specIndex)
    {
#if RESTAPI_R3
        source.WriteLine($"return global::R3.Observable.FromAsync(async ______ct =>");
#elif RESTAPI_REACTIVE
        source.WriteLine($"return global::Observables.RestAPI.Reactive.SystemReactiveObservableAdapter.FromAsync(async ______ct =>");
#else
#error Observables.RestAPI generator requires RESTAPI_R3 or RESTAPI_REACTIVE
#endif
        source.WriteLine("{");
        source.Indentation++;
        var args = FormatSendArgs(methodModel);
        var specField = SpecFieldName(specIndex);
        source.WriteLine($"return await global::Observables.RestAPI.RestApiBridge.SendAsync<{methodModel.ReturnResultType}, {methodModel.DeserializedResultType}>(Client, _settings, in {specField}, ______ct{args}).ConfigureAwait(false);");
        source.Indentation--;
        source.WriteLine("});");
    }

    static string FormatSendArgs(MethodModel methodModel)
    {
        var names = new List<string>();
        foreach (var param in methodModel.Parameters)
        {
            if (param.Kind == ParameterKind.CancellationToken)
                continue;
            names.Add("@" + param.MetadataName);
        }

        if (names.Count == 0)
            return "";
        return ", " + string.Join(", ", names);
    }

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
                    if (global::Observables.RestAPI.RestService.OwnsHttpClient(Client))
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

