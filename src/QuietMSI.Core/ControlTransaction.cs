namespace QuietMSI.Core;

public interface IControlTransport
{
    void VerifyIdentity();
    HardwareControlState ReadState();
    void Record(string stage, object data);
    void Write(RegisterChange change);
}

public sealed class ControlTransaction(IControlTransport transport)
{
    public ControlResult Apply(ControlRequest request)
    {
        try
        {
            transport.VerifyIdentity();
            var original = transport.ReadState();
            var plan = VerifiedControlPlan.Create(request, original);
            transport.Record("Original", new { request, original, plan });
            foreach (var change in plan)
            {
                var fresh = transport.ReadState();
                if (request.Kind == ControlKind.AutomaticFans && request.Value == "Advanced")
                {
                    if (!Same(original.CpuCurve, fresh.CpuCurve) || !Same(original.CpuBreakpoints, fresh.CpuBreakpoints))
                        throw new InvalidOperationException("Advanced curve dependencies changed before write; change denied.");
                    _ = VerifiedControlPlan.Create(request, fresh);
                }
                if (Value(fresh, change.Offset) != change.Before) throw new InvalidOperationException("Hardware state changed before Apply; no write performed for this step.");
                if (change.Before == change.After) continue;
                transport.Record("Pending", change);
                transport.Write(change);
                var after = transport.ReadState();
                transport.Record("Readback", new { change, after });
                if (Value(after, change.Offset) != change.After)
                    throw new InvalidOperationException("Hardware read-back mismatch. No retry or guessed rollback; inspect current state.");
            }
            var final = transport.ReadState();
            if (plan.Any(c => Value(final,c.Offset)!=c.After))
                throw new InvalidOperationException("Final complete state changed after a transaction step; success not confirmed.");
            if (request.Kind == ControlKind.AutomaticFans && request.Value == "Advanced"
                && (!Same(original.CpuCurve,final.CpuCurve) || !Same(original.CpuBreakpoints,final.CpuBreakpoints)))
                throw new InvalidOperationException("Final Advanced curve dependencies changed; state not confirmed.");
            transport.Record("Verified", new { request, final });
            return new(true, "Applied and verified by hardware read-back.");
        }
        catch (Exception e)
        {
            try { transport.Record("Failed", new { request, error = e.Message }); } catch { }
            return new(false, e.Message);
        }
    }
    private static bool Same(IReadOnlyList<int>? a,IReadOnlyList<int>? b) => a is not null && b is not null && a.SequenceEqual(b);
    private static byte? Value(HardwareControlState s, byte offset) => offset switch { 0x98 => s.Boost, 0xd2 => s.Performance, 0xd4 => s.FanMode, _ => null };
}
