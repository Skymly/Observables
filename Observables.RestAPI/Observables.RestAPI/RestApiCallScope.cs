using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Observables.RestAPI
{
    /// <summary>
    /// Per-invocation HTTP request options for generated REST proxies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Push"/> installs values for proxy methods entered while the scope is current.
    /// Generated proxies snapshot those values on entry and pass the snapshot into the request,
    /// so a later subscribe or thread-pool send still sees the options that were current at entry,
    /// even after this scope has been disposed.
    /// </para>
    /// <para>
    /// <see cref="Push"/> copies the dictionary structure immediately. Option values are shared by
    /// reference, so mutating a mutable value after <see cref="Push"/> still affects later binds
    /// that hold the snapshot.
    /// </para>
    /// <para>
    /// Nested scopes override inner keys and inherit outer keys. Dispose scopes in reverse order
    /// of <see cref="Push"/>. Disposing an outer scope while an inner scope is still current on the
    /// same async flow does not replace that inner scope. The disposed scope is removed from the
    /// chain, so a later pop does not restore it. Concurrent calls on different async flows do not
    /// see each other's snapshots.
    /// </para>
    /// <para>
    /// Precedence, lowest first: <see cref="RestApiSettings.HttpRequestMessageOptions"/>, then this
    /// snapshot, then a <see cref="PropertyAttribute"/> parameter on the same key.
    /// A null snapshot value clears that key. On .NET 6 and later,
    /// <c>HttpRequestMessage.Options.TryGetValue</c> does not surface a stored null. On
    /// netstandard2.0 the key is removed from <c>HttpRequestMessage.Properties</c>, so
    /// <c>ContainsKey</c> and <c>TryGetValue</c> agree with that clear.
    /// </para>
    /// </remarks>
    public static class RestApiCallScope
    {
        static readonly AsyncLocal<ScopeNode?> Current = new();

        /// <summary>
        /// Pushes call options for proxy methods entered before the returned scope is disposed.
        /// </summary>
        /// <param name="options">
        /// Options copied for this scope. Inner keys override the current scope. The dictionary is
        /// snapshotted immediately; later mutation of <paramref name="options"/> is ignored.
        /// </param>
        /// <returns>A disposable that restores the previous scope.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">An option key is <c>null</c>.</exception>
        public static IDisposable Push(IReadOnlyDictionary<string, object?> options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            foreach (var pair in options)
            {
                if (pair.Key is null)
                    throw new ArgumentException("HTTP request option keys must be non-null.", nameof(options));
            }

            var previous = Current.Value;
            var node = new ScopeNode(Freeze(previous?.Options, options), previous);
            Current.Value = node;
            return new Scope(node);
        }

        /// <summary>
        /// Returns the immutable options current on this async flow, or <c>null</c> when no scope is current.
        /// </summary>
        /// <remarks>Public because generated proxy code runs in the consumer assembly.</remarks>
        public static IReadOnlyDictionary<string, object?>? Capture() => Current.Value?.Options;

        static IReadOnlyDictionary<string, object?> Freeze(
            IReadOnlyDictionary<string, object?>? outer,
            IReadOnlyDictionary<string, object?> inner)
        {
            var copy = new Dictionary<string, object?>(
                (outer?.Count ?? 0) + inner.Count,
                StringComparer.Ordinal);
            if (outer != null)
            {
                foreach (var pair in outer)
                    copy[pair.Key] = pair.Value;
            }

            foreach (var pair in inner)
                copy[pair.Key] = pair.Value;

            return new ReadOnlyDictionary<string, object?>(copy);
        }

        sealed class ScopeNode
        {
            public ScopeNode(IReadOnlyDictionary<string, object?> options, ScopeNode? previous)
            {
                Options = options;
                Previous = previous;
            }

            public IReadOnlyDictionary<string, object?> Options { get; }

            public ScopeNode? Previous { get; set; }
        }

        sealed class Scope : IDisposable
        {
            readonly ScopeNode _node;
            int _disposed;

            public Scope(ScopeNode node) => _node = node;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                var head = Current.Value;
                if (ReferenceEquals(head, _node))
                {
                    Current.Value = _node.Previous;
                    return;
                }

                // Not the head: disposed out of order on this flow. Unlink so a later pop
                // skips this scope instead of restoring it. Previous is mutated without
                // synchronization: scopes are AsyncLocal-affine — each flow disposes its own
                // chain, so concurrent unlink on a chain shared across flows is unsupported.
                for (var cursor = head; cursor != null; cursor = cursor.Previous)
                {
                    if (ReferenceEquals(cursor.Previous, _node))
                    {
                        cursor.Previous = _node.Previous;
                        return;
                    }
                }
            }
        }
    }
}
