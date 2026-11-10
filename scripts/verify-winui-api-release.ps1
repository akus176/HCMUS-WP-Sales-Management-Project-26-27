[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/CoffeeShop.WinUI/CoffeeShop.WinUI.csproj'
$output = Join-Path $repositoryRoot 'src/CoffeeShop.WinUI/bin/Release/net10.0-windows10.0.19041.0/win-x64'

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        dotnet build $project --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'WinUI Release build failed.' }
    }

    $manifest = Join-Path $output 'CoffeeShop.WinUI.deps.json'
    $assembly = Join-Path $output 'CoffeeShop.WinUI.dll'
    if (-not (Test-Path -LiteralPath $manifest)) { throw "Release dependency manifest not found: $manifest" }
    if (-not (Test-Path -LiteralPath $assembly)) { throw "Release assembly not found: $assembly" }

    $dependencies = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    $forbidden = $dependencies.libraries.PSObject.Properties.Name |
        Where-Object { $_ -match 'CoffeeShop.Infrastructure.Postgres|Npgsql|EntityFrameworkCore' }
    if ($forbidden) { throw "API-only Release contains database dependencies: $($forbidden -join ', ')" }

    $forbiddenFiles = Get-ChildItem -LiteralPath $output -File |
        Where-Object { $_.Name -match 'CoffeeShop\.Infrastructure\.Postgres|Npgsql|EntityFrameworkCore' }
    if ($forbiddenFiles) { throw "API-only Release output contains database assemblies: $($forbiddenFiles.Name -join ', ')" }

    $assemblyText = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($assembly))
    foreach ($forbiddenSetting in @('ConnectionStrings__CoffeeShop', 'CoffeeShopDbContext', 'CoffeeShopPostgresOptions')) {
        if ($assemblyText.Contains($forbiddenSetting, [System.StringComparison]::Ordinal)) {
            throw "API-only Release assembly contains a PostgreSQL connection reference: $forbiddenSetting"
        }
    }

    Write-Output 'PASS: WinUI Release output contains no PostgreSQL infrastructure, Npgsql, Entity Framework, or PostgreSQL connection-setting references.'
    Write-Output "Manifest: $manifest"
    Write-Output "Assembly: $assembly"
}
finally {
    Pop-Location
}
