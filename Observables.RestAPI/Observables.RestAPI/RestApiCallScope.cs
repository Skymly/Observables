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
    /// Nested scopes override inner keys and inherit outer keys. Dispose scopes in reverse order
    /// of <see cref="Push"/>. Concurrent calls on different async flows do not see each other's snapshots.
    /// </para>
    /// <para>
    /// Precedence, lowest first: <see cref="RestApiSettings.HttpRequestMessageOptions"/>, then this
    /// snapshot, then a <see cref="PropertyAttribute"/> parameter on the same key.
    /// A null snapshot value clears that key for handlers using <c>HttpRequestMessage.Options.TryGetValue</c>,
    /// which does not surface a stored null.
    /// </para>
    /// </remarks>
    public static class RestApiCallScope
    {
        static readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> Current = new();

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
            Current.Value = Freeze(previous, options);
            return new Scope(previous);
        }

        /// <summary>
        /// Returns the immutable options current on this async flow, or <c>null</c> when no scope is current.
        /// </summary>
        /// <remarks>Public because generated proxy code runs in the consumer assembly.</remarks>
        public static IReadOnlyDictionary<string, object?>? Capture() => Current.Value;

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

        sealed class Scope : IDisposable
        {
            readonly IReadOnlyDictionary<string, object?>? _previous;
            int _disposed;

            public Scope(IReadOnlyDictionary<string, object?>? previous) => _previous = previous;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                Current.Value = _previous;
            }
        }
    }
}
