#Requires -Version 7.4

[CmdletBinding()]
param(
    [string]$PreviewRoot
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Run this launcher in PowerShell on Windows.' }

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$winUiOutput = 'src/CoffeeShop.WinUI/bin/Release/net10.0-windows10.0.19041.0/win-x64'

function Test-ApiReady([string]$ApiUrl) {
    try {
        $response = Invoke-WebRequest -Uri "$ApiUrl/health/ready" -TimeoutSec 2
        return $response.StatusCode -eq 200 -and $response.Content.Trim() -eq 'Healthy'
    }
    catch {
        return $false
    }
}

if ($PreviewRoot) {
    $PreviewRoot = (Resolve-Path -LiteralPath $PreviewRoot).ProviderPath
    $clientDirectory = Join-Path $PreviewRoot $winUiOutput
    $apiDirectory = Join-Path $PreviewRoot 'src/CoffeeShop.Api/bin/Release/net10.0'
    $clientExe = Join-Path $clientDirectory 'CoffeeShop.WinUI.exe'
    $apiAssembly = Join-Path $apiDirectory 'CoffeeShop.Api.dll'
    if (-not (Test-Path -LiteralPath $clientExe -PathType Leaf) -or
        -not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
        throw 'PreviewRoot must contain the built WinUI app and configured API.'
    }

    $apiUrl = 'http://127.0.0.1:5232'
    if (-not (Test-ApiReady $apiUrl)) {
        $apiLog = Join-Path $apiDirectory 'run-winui-api.log'
        Write-Host 'Starting the preview API…'
        $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList 'CoffeeShop.Api.dll', '--urls', $apiUrl `
            -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $apiLog -RedirectStandardError "$apiLog.err"

        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while (-not (Test-ApiReady $apiUrl)) {
            if ($apiProcess.HasExited -or [DateTime]::UtcNow -ge $deadline) {
                if (-not $apiProcess.HasExited) { Stop-Process -InputObject $apiProcess }
                throw "The preview API did not become ready. Check that its database is running and see $apiLog and $apiLog.err."
            }
            Start-Sleep -Seconds 1
        }
    }
}
else {
    if ($repositoryRoot.StartsWith('\\')) {
        throw 'WinUI builds require a checkout on a Windows drive. For an existing local build, use -PreviewRoot.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.env') -PathType Leaf)) {
        throw 'Create the local .env using the Windows setup in README.md, then run this launcher again.'
    }

    Push-Location -LiteralPath $repositoryRoot
    try {
        Write-Host 'Starting the API, database, and photo storage with Docker Compose…'
        docker compose up --build --detach --wait --wait-timeout 180 api
        if ($LASTEXITCODE -ne 0) { throw 'Docker Compose could not start the API. Check Docker Desktop and run docker compose ps.' }
        $publishedAddress = docker compose port api 8080
        if ($LASTEXITCODE -ne 0 -or $publishedAddress -notmatch '^127\.0\.0\.1:\d+$') {
            throw 'Could not determine the API port from Docker Compose.'
        }
        $apiUrl = "http://$publishedAddress"
        $photoAddress = docker compose port storage 9000
        if ($LASTEXITCODE -ne 0 -or $photoAddress -notmatch '^127\.0\.0\.1:\d+$') {
            throw 'Could not determine the photo storage port from Docker Compose.'
        }
        $env:COFFEESHOP_MENU_PHOTOS_BASE_URL = "http://$photoAddress/menu-images/"

        dotnet build src/CoffeeShop.WinUI -c Release -r win-x64 -p:WindowsAppSDKSelfContained=true
        if ($LASTEXITCODE -ne 0) { throw 'The WinUI build failed.' }
    }
    finally {
        Pop-Location
    }

    $clientDirectory = Join-Path $repositoryRoot $winUiOutput
    $clientExe = Join-Path $clientDirectory 'CoffeeShop.WinUI.exe'
}

Write-Host "Opening Corner Cafe with the API at $apiUrl."
Start-Process -FilePath $clientExe -WorkingDirectory $clientDirectory -Environment @{
    COFFEESHOP_CLIENT_MODE = 'api'
    COFFEESHOP_API_BASE_URL = "$apiUrl/api/v1/"
}
