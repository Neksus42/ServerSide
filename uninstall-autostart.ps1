$taskName = "PcControlAgent"
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
Remove-ItemProperty -Path $runKey -Name "PcControlAgent" -ErrorAction SilentlyContinue

Write-Host "PC Control autostart removed."
