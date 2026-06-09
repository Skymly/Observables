using System.Text;
using Google.Protobuf;
using Grpc.Core;

namespace Observables.Grpc;

/// <summary>Creates gRPC marshallers for supported message types.</summary>
public static class GrpcMarshallers
{
    public static readonly Marshaller<string> String = Marshallers.Create(
        static s => Encoding.UTF8.GetBytes(s),
        static data => Encoding.UTF8.GetString(data));

    public static Marshaller<T> ForMessage<T>()
        where T : class, IMessage<T>, new() =>
        Marshallers.Create(
            static (T message) => message.ToByteArray(),
            static data =>
            {
                var instance = new T();
                instance.MergeFrom(data);
                return instance;
            });
}
