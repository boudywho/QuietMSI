namespace QuietMSI.Core;

public static class ProtectedTelemetryPolicy
{
    public static bool SupportedModel(DeviceIdentity identity) =>
        identity.Manufacturer == "Micro-Star International Co., Ltd." && identity.Model == "GF63 Thin 11UCX";

    public static bool Supports(DeviceIdentity identity, byte[]? version) => SupportedModel(identity)
        && identity.EcFirmware == "16R6EMS1.106"
        && version is { Length: 32 } && version[0] == 1 && version[1] == 2 && version[2] == 8;
}
