namespace Observables.Grpc;

/// <summary>Marks a gRPC proxy interface for source generation.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class GrpcAttribute(string? serviceName = null) : Attribute
{
    public string? ServiceName { get; } = serviceName;
}

/// <summary>Unary RPC: single request, single response.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GrpcUnaryAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Server streaming RPC: single request, multiple responses.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GrpcServerStreamAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Client streaming RPC: request stream, single response.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GrpcClientStreamAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}

/// <summary>Duplex streaming RPC: bidirectional streams.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class GrpcDuplexAttribute(string? methodName = null) : Attribute
{
    public string? MethodName { get; } = methodName;
}
