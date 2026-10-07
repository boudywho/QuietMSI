using System.Management;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

internal static class MsiCpuCurve
{
    internal static byte[] ReadTable() => MsiControlStateReader.RequirePacket(MsiReadKind.CpuCurve).Skip(1).Take(8).ToArray();
    internal static void Write(byte[] expected, byte[] next, byte[] original)
    {
        if(expected.Length!=8 || next.Length!=8 || original.Length!=8 || next[0]!=expected[0] || next[7]!=original[7])
            throw new InvalidOperationException("Invalid curve or preserved bytes.");
        // Normal changes only increase original effort. Exact original restoration is a separate safe case.
        if(!CoolingCurvePlan.SameStableFields(next,original))
            _ = CoolingCurvePlan.Create(original,string.Join(',',next.Skip(1).Take(6)));
        var fresh=ReadTable();
        if(!CoolingCurvePlan.SameStableFields(fresh,expected)) throw new InvalidOperationException("CPU curve changed externally; refusing overwrite.");
        next[0]=fresh[0];
        var transport=new MsiControlTransport();
        transport.Record("PendingCpuCurve",new { Before=expected, After=next, Original=original });
        using var q=new ManagementObjectSearcher("root\\wmi","SELECT * FROM MSI_ACPI");
        using var rows=q.Get(); var instances=new List<ManagementObject>();
        try
        {
            foreach(ManagementObject row in rows)instances.Add(row);
            if(instances.Count!=1)throw new InvalidOperationException("Ambiguous MSI provider.");
            using var input=instances[0].GetMethodParameters("Set_Fan");
            using var schema=new ManagementClass("root\\wmi","Package_32",null);
            using var package=schema.CreateInstance() ?? throw new InvalidOperationException("Missing package schema.");
            var bytes=new byte[32];bytes[0]=1;next.CopyTo(bytes,1);package["Bytes"]=bytes;input["Data"]=package;
            using var reply=instances[0].InvokeMethod("Set_Fan",input,null);
            if(reply?["Data"] is not ManagementBaseObject data)throw new InvalidOperationException("No curve write confirmation; state indeterminate.");
            var returnValue=reply?.Properties.Cast<PropertyData>().FirstOrDefault(p=>p.Name=="ReturnValue")?.Value;
            using(data)if(!WmiResultRules.SetterSucceeded(returnValue,data["Bytes"] as byte[]))throw new InvalidOperationException("Curve setter failed; state may be indeterminate; no retry.");
            var after=ReadTable();transport.Record("CpuCurveReadback",new {After=after});
            if(!CoolingCurvePlan.SameStableFields(after,next))throw new InvalidOperationException("Curve read-back mismatch; no retry or guessed rollback.");
        }
        finally{foreach(var instance in instances)instance.Dispose();}
    }
}
