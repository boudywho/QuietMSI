using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using QuietMSI.Core;

namespace QuietMSI.Hardware;

internal static class FirmwareSupport
{
    internal const string ClassName = "QuietMSI_ACPI";
    internal const string PacketName = "QuietMSI_Packet32";
    private const string DriverKey = @"SYSTEM\CurrentControlSet\Services\WmiAcpi";
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "QuietMSI", "Support");

    // Runs in the elevated main process, never in a sampling worker. It installs
    // only our schema resource. Windows' signed ACPI driver is left intact.
    internal static void EnsureInstalled()
    {
        using var ownership = new Mutex(false, @"Global\QuietMSI.FirmwareSetup.v1");
        var acquired = false;
        try
        {
            try { acquired = ownership.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Another QuietMSI instance is preparing hardware support.");
            using var driver = Registry.LocalMachine.OpenSubKey(DriverKey, true)
                ?? throw new InvalidOperationException("Windows ACPI WMI support is unavailable.");
            var mof = Resource("QuietMSI.Firmware.mof");
            var dll = Resource("QuietMSI.Firmware.dll");
            var dllPath = Path.Combine(DirectoryPath, "QuietMSI.Firmware-" + Convert.ToHexString(SHA256.HashData(dll))[..16] + ".dll");
            var mofPath = Path.Combine(DirectoryPath, "QuietMSI.mof");
            var prior = driver.GetValue("MofImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            var priorKind = prior is null ? RegistryValueKind.ExpandString : driver.GetValueKind("MofImagePath");
            if (string.Equals(prior, dllPath, StringComparison.OrdinalIgnoreCase) && Matches(dllPath, dll) && Matches(mofPath, mof) && SchemaPresent())
                return;
            VerifySupportedModel();
            Directory.CreateDirectory(DirectoryPath);
            RejectLinks(DirectoryPath);
            if (!Matches(dllPath, dll)) AtomicWrite(dllPath, dll);
            if (!Matches(mofPath, mof)) AtomicWrite(mofPath, mof);
            ValidateResource(dllPath);
            Compile(mofPath, checkOnly: true);
            Compile(mofPath, checkOnly: false);
            // The same identity checks used before every hardware write are required
            // before taking ownership of the Windows schema registration.
            _ = MsiControlStateReader.VerifyIdentity();
            var backup = Path.Combine(DirectoryPath, "registration-backup.json");
            if (!File.Exists(backup))
                AtomicWrite(backup, JsonSerializer.SerializeToUtf8Bytes(new { PreviousMofImagePath = prior, PreviousKind = prior is null ? null : driver.GetValueKind("MofImagePath").ToString(), RecordedAt = DateTimeOffset.UtcNow }));
            try
            {
                driver.SetValue("MofImagePath", dllPath, RegistryValueKind.ExpandString);
                if (!string.Equals(driver.GetValue("MofImagePath") as string, dllPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Windows did not retain the hardware support registration.");
                _ = MsiControlStateReader.VerifyIdentity();
            }
            catch
            {
                if (prior is null) driver.DeleteValue("MofImagePath", false);
                else driver.SetValue("MofImagePath", prior, priorKind);
                throw;
            }
        }
        finally { if (acquired) ownership.ReleaseMutex(); }
    }

    private static void VerifySupportedModel()
    {
        using var query = new ManagementObjectSearcher("root\\cimv2", "SELECT Manufacturer,Model FROM Win32_ComputerSystem");
        using var rows = query.Get();
        foreach (ManagementObject row in rows)
        using (row)
            if (ProtectedTelemetryPolicy.SupportedModel(new(row["Manufacturer"]?.ToString() ?? "", row["Model"]?.ToString() ?? "", "", null))) return;
        throw new InvalidOperationException("Standalone firmware support is currently validated for MSI GF63 Thin 11UCX only.");
    }
    private static bool SchemaPresent()
    {
        try { using var schema = new ManagementClass("root\\wmi", ClassName, null); schema.Get(); return schema.Methods.Count == 9; }
        catch (ManagementException e) when (e.ErrorCode == ManagementStatus.InvalidClass || e.ErrorCode == ManagementStatus.NotFound) { return false; }
    }
    private static byte[] Resource(string name)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new IOException("Bundled hardware support is missing: " + name);
        using var buffer = new MemoryStream(); source.CopyTo(buffer); return buffer.ToArray();
    }
    private static bool Matches(string path, byte[] expected) => File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) && CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(path)), SHA256.HashData(expected));
    private static void RejectLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Hardware support cannot be installed through a redirected folder.");
    }
    private static void AtomicWrite(string path, byte[] data)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temporary, data); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void ValidateResource(string path)
    {
        var module = LoadLibraryEx(path, IntPtr.Zero, 2); // Data only; no DLL entry point or driver code.
        if (module == IntPtr.Zero) throw new IOException("Windows could not load the bundled firmware schema.");
        try
        {
            var resource = FindResource(module, "MofResource", "MOFDATA");
            if (resource == IntPtr.Zero || SizeofResource(module, resource) < 32)
                throw new IOException("The bundled native firmware schema resource is missing.");
        }
        finally { FreeLibrary(module); }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindResource(IntPtr module, string name, string type);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    private static void Compile(string path, bool checkOnly)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "wbem", "mofcomp.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (checkOnly) info.ArgumentList.Add("-check");
        info.ArgumentList.Add(path);
        using var process = Process.Start(info) ?? throw new IOException("Windows could not start hardware support setup.");
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(8000)) { process.Kill(true); process.WaitForExit(); throw new TimeoutException("Windows hardware support setup timed out."); }
        Task.WaitAll(output, errors);
        if (process.ExitCode != 0) throw new IOException("Windows hardware support setup failed: " + output.Result + errors.Result);
    }
}
