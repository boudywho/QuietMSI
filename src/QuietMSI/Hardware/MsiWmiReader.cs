using System.Management;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

internal static class MsiWmiReader
{
    internal static byte[]? ReadFirmwarePackage() => Read(MsiReadKind.Firmware);

    // Only four fixed read requests; no arbitrary string, register or setter API.
    internal static byte[]? Read(MsiReadKind kind)
    {
        var request = MsiReadRequest.Create(kind);
        using var searcher = new ManagementObjectSearcher(new ManagementScope("root\\wmi"), new ObjectQuery("SELECT * FROM MSI_ACPI"),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(3), ReturnImmediately = false, Rewindable = false });
        using var instances = searcher.Get();
        ManagementObject? selected = null;
        try
        {
            foreach (ManagementObject instance in instances)
            {
                if (selected is not null) { instance.Dispose(); throw new InvalidOperationException("Multiple MSI_ACPI instances; refusing ambiguous binding."); }
                selected = instance;
            }
            if (selected is null) return null;
            using var input = request.Payload is null ? null : selected.GetMethodParameters(request.Method);
            using var packageClass = request.Payload is null ? null : new ManagementClass("root\\wmi", "Package_32", null);
            using var package = packageClass?.CreateInstance();
            if (request.Payload is not null)
            {
                if (input is null || package is null) throw new InvalidOperationException("Expected embedded Package_32 request is unavailable.");
                package["Bytes"] = request.Payload;
                input["Data"] = package;
            }
            using var result = selected.InvokeMethod(request.Method, input, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(3) });
            if (result?["Data"] is not ManagementBaseObject data) return null;
            using (data)
            {
                var returnValue = result.Properties.Cast<PropertyData>().FirstOrDefault(p => p.Name == "ReturnValue")?.Value;
                if (returnValue is bool success && !success) return null;
                return data["Bytes"] is byte[] bytes ? (byte[])bytes.Clone() : null;
            }
        }
        finally { selected?.Dispose(); }
    }
}
