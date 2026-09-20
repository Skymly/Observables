using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR;

internal static class SignalRProtocol
{
    static readonly object MuxTableGate = new();
    static readonly ConditionalWeakTable<HubConnection, ConnectionMux> MuxTable = new();

    internal static async Task<T> InvokeAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        return await HubConnectionArgs.InvokeAsync<T>(connection, methodName, args, linked.Token).ConfigureAwait(false);
    }

    internal static async Task SendAsync(
        HubConnection connection,
        string methodName,
        object?[] args,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await HubConnectionArgs.SendAsync(connection, methodName, args, linked.Token).ConfigureAwait(false);
    }

    internal static async Task StreamAsync<T>(
        HubConnection connection,
        string methodName,
        object?[] args,
        Action<T> onNext,
        Action onCompleted,
        CancellationToken userToken,
        CancellationToken pumpToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, pumpToken);
        await foreach (var item in HubConnectionArgs
                           .StreamAsync<T>(connection, methodName, args, linked.Token)
                           .ConfigureAwait(false))
        {
            onNext(item);
        }

        onCompleted();
    }

    internal static IDisposable SubscribeOn<T>(HubConnection connection, string methodName, Action<T> onNext) =>
        GetMux(connection).Add(methodName, onNext);

    static ConnectionMux GetMux(HubConnection connection)
    {
        lock (MuxTableGate)
        {
            if (MuxTable.TryGetValue(connection, out var mux))
            {
                return mux;
            }

            mux = new ConnectionMux(connection);
            MuxTable.Add(connection, mux);
            return mux;
        }
    }

    sealed class ConnectionMux
    {
        readonly HubConnection _connection;
        readonly object _gate = new();
        readonly Dictionary<SlotKey, object> _slots = new();

        internal ConnectionMux(HubConnection connection) => _connection = connection;

        internal IDisposable Add<T>(string methodName, Action<T> onNext)
        {
            var key = new SlotKey(methodName, typeof(T));
            lock (_gate)
            {
                if (_slots.TryGetValue(key, out var existing) && existing is TypedSlot<T> existingSlot)
                {
                    existingSlot.Observers.Add(onNext);
                    return new Subscription<T>(this, key, onNext);
                }

                var created = new TypedSlot<T>(_gate);
                created.Registration = _connection.On<T>(methodName, created.FanOut);
                created.Observers.Add(onNext);
                _slots[key] = created;
                return new Subscription<T>(this, key, onNext);
            }
        }

        internal void Remove<T>(SlotKey key, Action<T> onNext)
        {
            IDisposable? registration = null;
            lock (_gate)
            {
                if (!_slots.TryGetValue(key, out var slot) || slot is not TypedSlot<T> typed)
                {
                    return;
                }

                typed.Observers.Remove(onNext);
                if (typed.Observers.Count != 0)
                {
                    return;
                }

                registration = typed.Registration;
                _slots.Remove(key);
            }

            registration?.Dispose();
        }

        sealed class TypedSlot<T>
        {
            readonly object _gate;
            internal readonly List<Action<T>> Observers = new();
            internal IDisposable? Registration;

            internal TypedSlot(object gate) => _gate = gate;

            internal void FanOut(T value)
            {
                Action<T>[] snapshot;
                lock (_gate)
                {
                    snapshot = Observers.ToArray();
                }

                for (var i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i](value);
                }
            }
        }

        sealed class Subscription<T>(ConnectionMux mux, SlotKey key, Action<T> onNext) : IDisposable
        {
            ConnectionMux? _mux = mux;

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _mux, null);
                owner?.Remove(key, onNext);
            }
        }
    }

    readonly struct SlotKey : IEquatable<SlotKey>
    {
        internal SlotKey(string methodName, Type payloadType)
        {
            MethodName = methodName;
            PayloadType = payloadType;
        }

        internal string MethodName { get; }
        internal Type PayloadType { get; }

        public bool Equals(SlotKey other) =>
            MethodName == other.MethodName && PayloadType == other.PayloadType;

        public override bool Equals(object? obj) => obj is SlotKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (MethodName.GetHashCode() * 397) ^ PayloadType.GetHashCode();
            }
        }
    }
}
