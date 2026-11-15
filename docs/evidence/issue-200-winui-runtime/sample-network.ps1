param(
    [Parameter(Mandatory)][string]$ClientPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter(Mandatory)][string]$StopFile
)

# Poll process-owned connections; this complements the request log and Release
# dependency scan. It is not a continuous packet capture.
while (!(Test-Path -LiteralPath $StopFile)) {
    $clients = Get-Process CoffeeShop.WinUI -ErrorAction SilentlyContinue |
        Where-Object Path -EQ $ClientPath
    foreach ($client in $clients) {
        $connections = @(Get-NetTCPConnection -OwningProcess $client.Id -ErrorAction SilentlyContinue |
            Where-Object RemotePort -GT 0 |
            Select-Object LocalPort, RemoteAddress, RemotePort, State)
        @{ at = [DateTime]::UtcNow.ToString('o'); pid = $client.Id; connections = $connections } |
            ConvertTo-Json -Compress -Depth 4 | Add-Content -LiteralPath $OutputPath
    }
    Start-Sleep -Seconds 1
}
