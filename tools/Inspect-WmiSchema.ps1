$ErrorActionPreference = 'Stop'
$schema = Get-CimClass -Namespace root/wmi -ClassName MSI_ACPI
$methods = foreach ($method in $schema.CimClassMethods) {
    if ($method.Name -notin @('Get_WMI','Get_EC','Get_Temperature','Get_Fan','Get_Data','Set_Data')) { continue }
    $parameters = foreach ($parameter in $method.Parameters) {
        [PSCustomObject]@{ Name=$parameter.Name; Type=$parameter.CimType.ToString(); Qualifiers=@($parameter.Qualifiers | Select-Object Name,Value) }
    }
    [PSCustomObject]@{ Name=$method.Name; Type=$method.ReturnType.ToString(); Qualifiers=@($method.Qualifiers | Select-Object Name,Value); Parameters=@($parameters) }
}
[PSCustomObject]@{
    ClassQualifiers=@($schema.CimClassQualifiers | Select-Object Name,Value)
    Properties=@($schema.CimClassProperties | Select-Object Name,CimType,@{Name='Qualifiers';Expression={@($_.Qualifiers | Select-Object Name,Value)}})
    Methods=@($methods)
    PacketQualifiers=@((Get-CimClass -Namespace root/wmi -ClassName Package_32).CimClassQualifiers | Select-Object Name,Value)
    PacketProperties=@((Get-CimClass -Namespace root/wmi -ClassName Package_32).CimClassProperties | Select-Object Name,CimType,@{Name='Qualifiers';Expression={@($_.Qualifiers | Select-Object Name,Value)}})
} | ConvertTo-Json -Depth 10
