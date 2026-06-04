namespace Observables.Mqtt.Generators;

internal sealed record MqttInterfaceModel(
    string FileName,
    string ClassName,
    string InterfaceDisplayName,
    string GeneratedNamespace,
    ImmutableEquatableArray<MqttMemberModel> Members,
    Nullability Nullability);

internal enum Nullability : byte
{
    Enabled,
    Disabled,
    None,
}
