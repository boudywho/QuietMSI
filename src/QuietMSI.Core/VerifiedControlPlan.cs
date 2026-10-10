namespace QuietMSI.Core;

public sealed record RegisterChange(byte Offset, byte Before, byte After);
public static class VerifiedControlPlan
{
    public static IReadOnlyList<RegisterChange> Create(ControlRequest request, HardwareControlState state)
    {
        var changes = new List<RegisterChange>();
        switch (request.Kind)
        {
            case ControlKind.CoolerBoost:
                if (!bool.TryParse(request.Value, out var enabled)) throw new ArgumentException("Cooler Boost must be true or false.");
                var boost = state.Boost ?? throw new InvalidOperationException("Missing successful Cooler Boost pre-read.");
                changes.Add(new(0x98, boost, (byte)(enabled ? boost | 0x80 : boost & 0x7f)));
                break;
            case ControlKind.PerformanceProfile:
                var current = state.Performance ?? throw new InvalidOperationException("Missing performance pre-read.");
                if (current is not (0xc0 or 0xc1 or 0xc2 or 0xc4)) throw new InvalidOperationException("Current firmware performance encoding is unsupported.");
                byte value = request.Value switch { "Turbo" => 0xc4, "High" => 0xc0, "Medium" => 0xc1, "Eco" => 0xc2, _ => throw new ArgumentException("Unsupported performance level.") };
                changes.Add(new(0xd2, current, value));
                break;
            case ControlKind.AutomaticFans:
                if (request.Value is not ("Auto" or "Advanced")) throw new ArgumentException("Only Auto or Advanced mode is approved.");
                var mode = state.FanMode ?? throw new InvalidOperationException("Missing fan-mode pre-read.");
                if (mode is not (0x0d or 0x8d)) throw new InvalidOperationException("Unsupported fan mode baseline; Auto change denied.");
                var oldBoost = state.Boost ?? throw new InvalidOperationException("Missing Cooler Boost pre-read.");
                if (request.Value == "Advanced")
                {
                    if (state.CpuCurve is not { Count: 6 } values || state.CpuBreakpoints is not { Count: 6 })
                        throw new InvalidOperationException("Current firmware curve unavailable; Advanced denied.");
                    for (var i=0;i<6;i++) if (values[i] is <20 or >100 || i>0 && values[i]<values[i-1])
                        throw new InvalidOperationException("Current curve violates conservative fan-safety bounds; Advanced denied.");
                }
                changes.Add(new(0xd4, mode, (byte)(request.Value == "Advanced" ? mode | 0x80 : mode & 0x7f)));
                changes.Add(new(0x98, oldBoost, (byte)(oldBoost & 0x7f)));
                break;
            default: throw new InvalidOperationException("This hardware control has no verified write plan.");
        }
        return changes;
    }
}
