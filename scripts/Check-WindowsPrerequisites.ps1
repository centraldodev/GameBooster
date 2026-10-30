# Read-only environment checks. Run with Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
[pscustomobject]@{
    Windows = [Environment]::OSVersion.VersionString
    PowerShell = $PSVersionTable.PSVersion.ToString()
    Administrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    ExplorerAvailable = [bool](Get-Process explorer -ErrorAction SilentlyContinue)
}
$commands = @('Get-CimInstance', 'Get-NetAdapter', 'Get-DnsClientServerAddress', 'Set-DnsClientServerAddress', 'New-NetQosPolicy', 'Remove-NetQosPolicy', 'Checkpoint-Computer', 'Get-ComputerRestorePoint', 'Get-Printer', 'Get-PrintJob', 'Clear-DnsClientCache')
foreach ($name in $commands) {
    [pscustomobject]@{ Command = $name; Available = [bool](Get-Command $name -ErrorAction SilentlyContinue) }
}
& "$env:SystemRoot\System32\powercfg.exe" /getactivescheme
Get-NetAdapter -ErrorAction Continue | Select-Object Name, Status, InterfaceGuid
