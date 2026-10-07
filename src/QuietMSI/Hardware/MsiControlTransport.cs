using System.Management;
using System.Text;
using System.Text.Json;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

internal sealed class MsiControlTransport : IControlTransport
{
    public void VerifyIdentity() => MsiControlStateReader.VerifyIdentity();
    public HardwareControlState ReadState() => MsiControlStateReader.Read();
    public void Record(string stage, object data)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietMSI", "logs");
        Directory.CreateDirectory(directory);
        var line = JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, stage, data }) + Environment.NewLine;
        using var file = new FileStream(Path.Combine(directory, "hardware-changes.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read);
        file.Write(Encoding.UTF8.GetBytes(line)); file.Flush(flushToDisk: true);
    }
    public void Write(RegisterChange change)
    {
        // This is not a generic EC writer: admit only transformations traced for this hardware.
        var valid = change.Offset switch
        {
            0x98 => (change.Before & 0x7f) == (change.After & 0x7f),
            0xd2 => (change.Before is 0xc0 or 0xc1 or 0xc2 or 0xc4) && (change.After is 0xc0 or 0xc1 or 0xc2 or 0xc4),
            0xd4 => (change.Before & 0x6f) == 0x0d && (change.After is 0x0d or 0x8d),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("Register transformation is not allowlisted.");
        using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM MSI_ACPI");
        using var rows = searcher.Get();
        var list = new List<ManagementObject>();
        try
        {
            foreach (ManagementObject row in rows) list.Add(row);
            if (list.Count != 1) throw new InvalidOperationException("Ambiguous or missing MSI_ACPI instance; write denied.");
            var instance = list[0];
            using var input = instance.GetMethodParameters("Set_Data");
            using var schema = new ManagementClass("root\\wmi", "Package_32", null);
            using var package = schema.CreateInstance() ?? throw new InvalidOperationException("Package schema unavailable.");
            var payload = new byte[32]; payload[0] = change.Offset; payload[1] = change.After;
            package["Bytes"] = payload; input["Data"] = package;
            using var result = instance.InvokeMethod("Set_Data", input, null);
            if (result?["Data"] is not ManagementBaseObject data) throw new InvalidOperationException("No setter confirmation; hardware state may have changed.");
            var returnValue = result?.Properties.Cast<PropertyData>().FirstOrDefault(p => p.Name=="ReturnValue")?.Value;
            using (data)
                if (!WmiResultRules.SetterSucceeded(returnValue,data["Bytes"] as byte[]))
                    throw new InvalidOperationException("Setter status not successful; no retry.");
        }
        finally { foreach (var row in list) row.Dispose(); }
    }
}
