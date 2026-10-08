param([Parameter(Mandatory)][string]$Dll, [Parameter(Mandatory)][string]$ExpectedBinary, [Parameter(Mandatory)][string]$Result)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class FirmwareResourceCheck {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr LoadLibraryEx(string file, IntPtr unused, uint flags);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr FindResource(IntPtr module, string name, string type);
  [DllImport("kernel32.dll")] public static extern uint SizeofResource(IntPtr module, IntPtr resource);
  [DllImport("kernel32.dll")] public static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
  [DllImport("kernel32.dll")] public static extern IntPtr LockResource(IntPtr resource);
  [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
}
'@
$module = [FirmwareResourceCheck]::LoadLibraryEx($Dll, [IntPtr]::Zero, 2)
if ($module -eq [IntPtr]::Zero) { throw 'Windows could not load the owned resource as data.' }
try {
    $resource = [FirmwareResourceCheck]::FindResource($module, 'MofResource', 'MOFDATA')
    if ($resource -eq [IntPtr]::Zero) { throw ('Native MOFDATA/MofResource is missing. Win32 error ' + [Runtime.InteropServices.Marshal]::GetLastWin32Error()) }
    $size = [FirmwareResourceCheck]::SizeofResource($module, $resource)
    $loaded = [FirmwareResourceCheck]::LoadResource($module, $resource)
    $pointer = [FirmwareResourceCheck]::LockResource($loaded)
    $bytes = [byte[]]::new($size)
    [Runtime.InteropServices.Marshal]::Copy($pointer, $bytes, 0, $size)
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    if ($hash -ne (Get-FileHash -LiteralPath $ExpectedBinary).Hash) { throw 'Resource differs from the independently compiled MOF.' }
    [PSCustomObject]@{ Success=$true; ResourceType='MOFDATA'; Name='MofResource'; Size=$size; Sha256=$hash; LoadMode='LOAD_LIBRARY_AS_DATAFILE; no executable code run' } | ConvertTo-Json | Set-Content -LiteralPath $Result
} finally { [FirmwareResourceCheck]::FreeLibrary($module) | Out-Null }

