using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;

namespace Observables.SignalR.Tests.Infrastructure;

internal static class HubConnectionOnHandlers
{
    static readonly FieldInfo HandlersField =
        typeof(HubConnection).GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("HubConnection._handlers is missing.");

    internal static int Count(HubConnection connection, string methodName)
    {
        var handlers = HandlersField.GetValue(connection) as IDictionary;
        if (handlers is null || !handlers.Contains(methodName))
        {
            return 0;
        }

        var list = handlers[methodName];
        if (list is null)
        {
            return 0;
        }

        var getHandlers = list.GetType().GetMethod(
            "GetHandlers",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (getHandlers?.Invoke(list, null) is not Array copied)
        {
            return 0;
        }

        return copied.Length;
    }
}
