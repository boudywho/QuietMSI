namespace QuietMSI.Core;

public static class HardwarePolicy
{
    public static Capability GetCapability(DeviceIdentity identity, ControlKind kind)
    {
        if (string.IsNullOrWhiteSpace(identity.EcFirmware))
            return new(kind, false, "EC firmware is not identified. Hardware changes are locked.");
        return new(kind, false, "This firmware has no fully verified writable policy. Hardware changes are locked.");
    }
}
