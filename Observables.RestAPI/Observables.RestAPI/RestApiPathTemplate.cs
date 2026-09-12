using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Observables.RestAPI
{
    internal enum RestApiDeclaredKind : byte
    {
        None = 0,
        Path,
        Query,
        Body,
        Header,
        HeaderCollection,
        Authorize,
        Property,
        Multipart,
        Cancellation,
    }

    internal readonly struct RestApiParameterSlot
    {
        public RestApiParameterSlot(string name, RestApiDeclaredKind declaredKind)
        {
            Name = name ?? "";
            DeclaredKind = declaredKind;
        }

        public string Name { get; }
        public RestApiDeclaredKind DeclaredKind { get; }
    }

    internal sealed class RestApiOccupancy
    {
        public RestApiOccupancy(
            HashSet<string> placeholders,
            HashSet<string> pathKindNames,
            HashSet<string> unresolvedPlaceholders,
            HashSet<string> explicitNonPathInPath)
        {
            Placeholders = placeholders;
            PathKindNames = pathKindNames;
            UnresolvedPlaceholders = unresolvedPlaceholders;
            ExplicitNonPathInPath = explicitNonPathInPath;
        }

        public HashSet<string> Placeholders { get; }
        public HashSet<string> PathKindNames { get; }
        public HashSet<string> UnresolvedPlaceholders { get; }
        public HashSet<string> ExplicitNonPathInPath { get; }

        public bool Matches =>
            UnresolvedPlaceholders.Count == 0
            && ExplicitNonPathInPath.Count == 0
            && Placeholders.SetEquals(PathKindNames);
    }

    internal sealed class RestApiPathTemplate
    {
        RestApiPathTemplate(string raw, HashSet<string> placeholders)
        {
            Raw = raw;
            Placeholders = placeholders;
        }

        public string Raw { get; }
        public HashSet<string> Placeholders { get; }

        public static RestApiPathTemplate Parse(string? rawPath)
        {
            var raw = rawPath ?? "";
            return new RestApiPathTemplate(raw, ExtractPlaceholders(raw));
        }

        public RestApiOccupancy Occupy(IReadOnlyList<RestApiParameterSlot> slots)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var pathKindNames = new HashSet<string>(StringComparer.Ordinal);
            var explicitNonPathInPath = new HashSet<string>(StringComparer.Ordinal);
            var unresolved = new HashSet<string>(Placeholders, StringComparer.Ordinal);

            foreach (var slot in slots)
            {
                names.Add(slot.Name);
                if (!Placeholders.Contains(slot.Name))
                    continue;

                unresolved.Remove(slot.Name);
                if (IsPathEligible(slot.DeclaredKind))
                    pathKindNames.Add(slot.Name);
                else
                    explicitNonPathInPath.Add(slot.Name);
            }

            return new RestApiOccupancy(Placeholders, pathKindNames, unresolved, explicitNonPathInPath);
        }

        public bool Matches(IReadOnlyList<RestApiParameterSlot> slots) => Occupy(slots).Matches;

        public string Sync(IReadOnlyList<RestApiParameterSlot> slots)
        {
            var occupancy = Occupy(slots);
            var path = Raw;

            var unmarkedNotInPath = new List<string>();
            foreach (var slot in slots)
            {
                if (slot.DeclaredKind == RestApiDeclaredKind.None && !Placeholders.Contains(slot.Name))
                    unmarkedNotInPath.Add(slot.Name);
            }

            if (occupancy.UnresolvedPlaceholders.Count == 1 && unmarkedNotInPath.Count == 1)
            {
                var unresolved = occupancy.UnresolvedPlaceholders.First();
                path = ReplacePlaceholderName(path, unresolved, unmarkedNotInPath[0]);
                occupancy = Parse(path).Occupy(slots);
            }

            foreach (var extra in occupancy.UnresolvedPlaceholders)
                path = RemovePlaceholder(path, extra);
            foreach (var extra in occupancy.ExplicitNonPathInPath)
                path = RemovePlaceholder(path, extra);

            return path;
        }

        public static string Suggest(string methodName, IReadOnlyList<RestApiParameterSlot> slots)
        {
            var names = new List<string>();
            foreach (var slot in slots)
            {
                if (slot.DeclaredKind == RestApiDeclaredKind.None)
                    names.Add(slot.Name);
            }

            if (names.Count == 0)
                return "/" + (methodName ?? "").ToLowerInvariant();

            var sb = new StringBuilder();
            foreach (var name in names)
            {
                sb.Append('/');
                sb.Append('{');
                sb.Append(name);
                sb.Append('}');
            }

            return sb.ToString();
        }

        internal static HashSet<string> ExtractPlaceholders(string path)
        {
            var placeholders = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] != '{')
                    continue;

                var close = path.IndexOf('}', i + 1);
                if (close < 0)
                    break;

                var name = path.Substring(i + 1, close - i - 1);
                if (name.Length > 0)
                    placeholders.Add(name);
                i = close;
            }

            return placeholders;
        }

        static bool IsPathEligible(RestApiDeclaredKind kind) =>
            kind is RestApiDeclaredKind.None or RestApiDeclaredKind.Path;

        static string ReplacePlaceholderName(string path, string from, string to)
        {
            var token = "{" + from + "}";
            var replacement = "{" + to + "}";
            var index = path.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return path;
            return path.Substring(0, index) + replacement + path.Substring(index + token.Length);
        }

        static string RemovePlaceholder(string path, string placeholder)
        {
            var token = "{" + placeholder + "}";
            var index = path.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                return path;

            var before = path.Substring(0, index);
            var after = path.Substring(index + token.Length);

            if (before.EndsWith("/", StringComparison.Ordinal))
                before = before.Substring(0, before.Length - 1);

            if (after.StartsWith("/", StringComparison.Ordinal))
                after = after.Substring(1);

            if (before.Length == 0)
                return string.IsNullOrEmpty(after) ? "" : "/" + after.TrimStart('/');

            if (after.Length == 0)
                return before;

            if (before.EndsWith("/", StringComparison.Ordinal) || after.StartsWith("/", StringComparison.Ordinal))
                return before + after;

            return before + "/" + after;
        }
    }
}
