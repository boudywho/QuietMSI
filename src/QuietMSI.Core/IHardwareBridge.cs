namespace QuietMSI.Core;

public interface IHardwareBridge
{
    Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken);
    Task<ControlResult> ApplyAsync(ControlRequest request, CancellationToken cancellationToken);
}

public class ReadOnlyBridge : IHardwareBridge
{
    public virtual Task<HardwareSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HardwareSnapshot(new("Unknown", "Unknown", "Unknown", null), DateTimeOffset.UtcNow,
            null, null, FanReading.FromTarget(null, "Unavailable"), FanReading.FromTarget(null, "Unavailable"), null, null,
            "Provider unavailable", Array.Empty<string>()));
    }

    public Task<ControlResult> ApplyAsync(ControlRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ControlResult(false, "This build is read-only. No hardware change was attempted."));
    }
}
