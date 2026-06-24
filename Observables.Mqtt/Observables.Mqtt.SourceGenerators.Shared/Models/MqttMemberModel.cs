namespace Observables.Mqtt.Generators;

internal sealed record MqttMemberModel(
    string MemberName,
    string TopicTemplate,
    MqttBoundaryKind BoundaryKind,
    bool IsProperty,
    string ReturnTypeDisplay,
    string ResultTypeDisplay,
    ImmutableEquatableArray<string> ParameterDeclarations,
    ImmutableEquatableArray<string> TopicParameterNames,
    bool HasCancellationToken);
