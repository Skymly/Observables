using System.Collections.Concurrent;
using System.ComponentModel;
using Grpc.Core;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Grpc;

/// <summary>Creates source-generated gRPC proxy implementations.</summary>
public static class GrpcService
{
    static readonly ConcurrentDictionary<Type, Func<CallInvoker, object>> GeneratedFactories = new();

    /// <summary>Registers a source-generated gRPC proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterGeneratedFactory(Type grpcInterfaceType, Func<CallInvoker, object> factory)
    {
        if (grpcInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(grpcInterfaceType));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        GeneratedFactories[grpcInterfaceType] = factory;
    }

#if NET8_0_OR_GREATER
    public static T For<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] T>(CallInvoker invoker) => (T)For(typeof(T), invoker);
#else
    public static T For<T>(CallInvoker invoker) => (T)For(typeof(T), invoker);
#endif

#if NET8_0_OR_GREATER
    public static object For(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type grpcInterfaceType,
        CallInvoker invoker
    )
#else
    public static object For(Type grpcInterfaceType, CallInvoker invoker)
#endif
    {
        if (grpcInterfaceType is null)
        {
            throw new ArgumentNullException(nameof(grpcInterfaceType));
        }

        if (invoker is null)
        {
            throw new ArgumentNullException(nameof(invoker));
        }

        if (GeneratedFactories.TryGetValue(grpcInterfaceType, out var factory))
        {
            return factory(invoker);
        }

        throw new InvalidOperationException(
            grpcInterfaceType.Name
            + " does not have a generated gRPC proxy. Ensure the interface is marked with [Grpc], "
            + "Observables.Grpc source generators are referenced, and the project was rebuilt.");
    }
}
