using System.Collections;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#endif

namespace Observables.RestAPI;

/// <summary>
/// Transforms a form source from a .NET representation to the appropriate HTTP form encoded representation.
/// </summary>
/// <remarks>Performs field renaming and value formatting as specified in <see cref="QueryAttribute"/>s and
/// <see cref="RestApiSettings.FormUrlEncodedParameterFormatter"/>. A given key may appear multiple times with the
/// same or different values.</remarks>
class FormValueMultimap : IEnumerable<KeyValuePair<string?, string?>>
{
    static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = [];

    readonly IList<KeyValuePair<string?, string?>> formEntries = [];

    readonly IHttpContentSerializer contentSerializer;

    public FormValueMultimap(object source, RestApiSettings settings)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));
        contentSerializer = settings.ContentSerializer;

        if (source == null)
            return;

        if (source is IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                var value = dictionary[key];
                if (value != null)
                {
                    Add(
                        key.ToString(),
                        settings.FormUrlEncodedParameterFormatter.Format(value, null)
                    );
                }
            }

            return;
        }

#if NET8_0_OR_GREATER
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException("Native AOT URL-encoded forms require a string or a dictionary of form fields.");
        }
#endif

        AddObjectProperties(source, settings);
    }

#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Callers using the legacy form-object path must preserve the DTO's public properties. Native AOT uses the dictionary path.")]
#endif
    void AddObjectProperties(object source, RestApiSettings settings)
    {
        var type = source.GetType();

        PropertyInfo[] properties;
        lock (PropertyCache)
        {
            if (!PropertyCache.TryGetValue(type, out properties!))
            {
                properties = GetProperties(type);
                PropertyCache[type] = properties;
            }
        }

        foreach (var property in properties)
        {
            var value = property.GetValue(source, null);
            if (value == null)
                continue;

            var fieldName = GetFieldNameForProperty(property);

            // see if there's a query attribute
            var attrib = property.GetCustomAttribute<QueryAttribute>(true);

            // add strings/non enumerable properties
            if (value is not IEnumerable enumerable || value is string)
            {
                Add(
                    fieldName,
                    settings.FormUrlEncodedParameterFormatter.Format(value, attrib?.Format)
                );
                continue;
            }

            var collectionFormat =
                attrib != null && attrib.IsCollectionFormatSpecified
                    ? attrib.CollectionFormat
                    : settings.CollectionFormat;

            switch (collectionFormat)
            {
                case CollectionFormat.Multi:
                    foreach (var item in enumerable)
                    {
                        Add(
                            fieldName,
                            settings.FormUrlEncodedParameterFormatter.Format(
                                item,
                                attrib?.Format
                            )
                        );
                    }

                    break;
                case CollectionFormat.Csv:
                case CollectionFormat.Ssv:
                case CollectionFormat.Tsv:
                case CollectionFormat.Pipes:
                    var delimiter = collectionFormat switch
                    {
                        CollectionFormat.Csv => ",",
                        CollectionFormat.Ssv => " ",
                        CollectionFormat.Tsv => "\t",
                        _ => "|"
                    };

                    var formattedValues = enumerable
                        .Cast<object>()
                        .Select(
                            v =>
                                settings.FormUrlEncodedParameterFormatter.Format(
                                    v,
                                    attrib?.Format
                                )
                        );
                    Add(fieldName, string.Join(delimiter, formattedValues));
                    break;
                default:
                    Add(
                        fieldName,
                        settings.FormUrlEncodedParameterFormatter.Format(
                            value,
                            attrib?.Format
                        )
                    );
                    break;
            }
        }
    }

    /// <summary>
    /// Returns a key for each entry. If multiple entries share the same key, the key is returned multiple times.
    /// </summary>
    public IEnumerable<string?> Keys => this.Select(it => it.Key);

    void Add(string? key, string? value)
    {
        formEntries.Add(new KeyValuePair<string?, string?>(key, value));
    }

    string GetFieldNameForProperty(PropertyInfo propertyInfo)
    {
        var name =
            propertyInfo
                .GetCustomAttributes<AliasAsAttribute>(true)
                .Select(a => a.Name)
                .FirstOrDefault()
            ?? contentSerializer.GetFieldNameForProperty(propertyInfo)
            ?? propertyInfo.Name;

        var qattrib = propertyInfo
            .GetCustomAttributes<QueryAttribute>(true)
            .Select(
                attr =>
                    !string.IsNullOrWhiteSpace(attr.Prefix)
                        ? $"{attr.Prefix}{attr.Delimiter}{name}"
                        : name
            )
            .FirstOrDefault();

        return qattrib ?? name;
    }

    static PropertyInfo[] GetProperties(
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        Type type)
    {
        return [.. type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetMethod?.IsPublic == true)];
    }

    public IEnumerator<KeyValuePair<string?, string?>> GetEnumerator()
    {
        return formEntries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
