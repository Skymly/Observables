using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Observables.Postgres;

/// <summary>Creates source-generated PostgreSQL LISTEN/NOTIFY proxy implementations.</summary>
public static class PostgresService
{
    /// <summary>Registers a source-generated channel proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type postgresInterfaceType,
        Func<NpgsqlConnection, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<NpgsqlConnection>.Register(postgresInterfaceType, factory);

    /// <summary>
    /// Creates a generated proxy for <typeparamref name="T"/> using a dedicated, non-pooled
    /// <see cref="NpgsqlConnection"/>. Do not pass a connection taken from
    /// <c>NpgsqlDataSource</c> / the pool for long-lived Listen (<c>Wait</c>) loops.
    /// Prefer <c>Pooling=false</c> and keepalive on the Listen connection.
    /// The connection is not disposed by the proxy; its lifetime should match Listen subscriptions.
    /// </summary>
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(NpgsqlConnection connection) => (T)For(typeof(T), connection);

    /// <inheritdoc cref="For{T}(NpgsqlConnection)"/>
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type postgresInterfaceType,
        NpgsqlConnection connection)
    {
        if (postgresInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(postgresInterfaceType));
        }

        return global::Observables.GeneratedProxyFactoryRegistry<NpgsqlConnection>.Create(
            postgresInterfaceType,
            connection,
            postgresInterfaceType.Name
            + " does not have a generated Postgres proxy. Ensure the interface is marked with [Postgres], "
            + "Observables.Postgres source generators are referenced, and the project was rebuilt.");
    }
}
