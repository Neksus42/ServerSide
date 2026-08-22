param([int]$Port = 8888)

$ruleName = "PC Control $Port"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
    Write-Host "Created Private-network firewall rule for TCP $Port."
} else {
    Write-Host "Firewall rule '$ruleName' already exists."
}
