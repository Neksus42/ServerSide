param(
    [Parameter(Mandatory=$true)]
    [string]$ExePath
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from PowerShell as Administrator."
}

$resolved = (Resolve-Path $ExePath).Path
$workingDirectory = Split-Path -Parent $resolved
$user = "$env:USERDOMAIN\$env:USERNAME"
$taskName = "PcControlAgent"

# Remove the old HKCU Run entry if an earlier version created it.
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "PcControlAgent" -ErrorAction SilentlyContinue

$action = New-ScheduledTaskAction `
    -Execute $resolved `
    -WorkingDirectory $workingDirectory

$trigger = New-ScheduledTaskTrigger -AtLogOn -User $user

$taskPrincipal = New-ScheduledTaskPrincipal `
    -UserId $user `
    -LogonType Interactive `
    -RunLevel Highest

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew

$task = New-ScheduledTask `
    -Action $action `
    -Trigger $trigger `
    -Principal $taskPrincipal `
    -Settings $settings

Register-ScheduledTask `
    -TaskName $taskName `
    -InputObject $task `
    -Force | Out-Null

Write-Host "Autostart installed: $taskName"
Write-Host "The agent will start silently with highest privileges when $user signs in."
Write-Host "Starting it now..."
Start-ScheduledTask -TaskName $taskName
