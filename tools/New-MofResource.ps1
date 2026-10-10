param([Parameter(Mandatory)][string]$Mof, [Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$binary = [IO.Path]::ChangeExtension($Output, '.bmf')
& "$env:windir\System32\wbem\mofcomp.exe" "-B:$binary" $Mof
if ($LASTEXITCODE -ne 0) { throw 'Firmware schema compilation failed.' }
$data = [IO.File]::ReadAllBytes($binary)
$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream)
function Align { while ($stream.Position % 4) { $writer.Write([byte]0) } }
function ResourceHeader([uint32]$size, [string]$type, [string]$name) {
    $begin = $stream.Position
    $writer.Write($size); $writer.Write([uint32]0)
    if ($type) { foreach ($char in $type.ToCharArray()) { $writer.Write([uint16]$char) }; $writer.Write([uint16]0) }
    else { $writer.Write([uint16]65535); $writer.Write([uint16]0) }
    if ($name) { foreach ($char in $name.ToCharArray()) { $writer.Write([uint16]$char) }; $writer.Write([uint16]0) }
    else { $writer.Write([uint16]65535); $writer.Write([uint16]0) }
    Align
    $writer.Write([uint32]0); $writer.Write([uint16]0x30); $writer.Write([uint16]0x409)
    $writer.Write([uint32]0); $writer.Write([uint32]0)
    $end = $stream.Position
    $stream.Position = $begin + 4; $writer.Write([uint32]($end - $begin)); $stream.Position = $end
}
ResourceHeader 0 '' ''
# Native FindResource normalizes string identifiers to uppercase, as rc.exe does.
ResourceHeader $data.Length 'MOFDATA' 'MOFRESOURCE'
$writer.Write($data); Align
[IO.File]::WriteAllBytes($Output, $stream.ToArray())
$writer.Dispose(); $stream.Dispose()
