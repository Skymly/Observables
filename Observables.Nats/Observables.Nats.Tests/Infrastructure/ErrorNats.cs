using System.Reflection;
using NATS.Client.Core;

namespace Observables.Nats.Tests.Infrastructure;

internal static class ErrorNats
{
    public static INatsConnection CreateWithDeserializeError()
    {
        var connection = DispatchProxy.Create<INatsConnection, ErrorNatsProxy>();
        ((ErrorNatsProxy)(object)connection).Message = CreateDeserializeError();
        return connection;
    }

    public static NatsMsg<string> CreateDeserializeError()
    {
        var headers = new NatsHeaders();
        var error = new NatsDeserializeException(
            Array.Empty<byte>(),
            new InvalidOperationException("nats-deserialize-failed"));
        typeof(NatsHeaders)
            .GetProperty(nameof(NatsHeaders.Error), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(headers, [error]);

        return new NatsMsg<string>("orders", replyTo: null, size: 0, headers, data: default, connection: null);
    }

    public class ErrorNatsProxy : DispatchProxy
    {
        public NatsMsg<string> Message { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(INatsConnection.SubscribeAsync))
            {
                return Yield(Message);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }

        static async IAsyncEnumerable<NatsMsg<string>> Yield(NatsMsg<string> message)
        {
            yield return message;
        }
    }
}
