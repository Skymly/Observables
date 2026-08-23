using System.ComponentModel;
using Grpc.Core;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Observables.Grpc;

/// <summary>Creates source-generated gRPC proxy implementations.</summary>
public static class GrpcService
{
    /// <summary>Registers a source-generated gRPC proxy factory.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET8_0_OR_GREATER
    public static void RegisterGeneratedFactory(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties
        )] Type grpcInterfaceType,
        Func<CallInvoker, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<CallInvoker>.Register(grpcInterfaceType, factory);
#else
    public static void RegisterGeneratedFactory(Type grpcInterfaceType, Func<CallInvoker, object> factory) =>
        global::Observables.GeneratedProxyFactoryRegistry<CallInvoker>.Register(grpcInterfaceType, factory);
#endif

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

        return global::Observables.GeneratedProxyFactoryRegistry<CallInvoker>.Create(
            grpcInterfaceType,
            invoker,
            grpcInterfaceType.Name
            + " does not have a generated gRPC proxy. Ensure the interface is marked with [Grpc], "
            + "Observables.Grpc source generators are referenced, and the project was rebuilt.");
    }
}
