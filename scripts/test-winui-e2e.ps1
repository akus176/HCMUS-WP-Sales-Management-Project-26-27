#Requires -Version 7.4

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Run this fixture on a disposable GitHub-hosted Windows runner.'
}

$repository = Split-Path -Parent $PSScriptRoot
$results = Join-Path $repository 'TestResults/WinUI-E2E'
$apiAssembly = Join-Path $repository 'src/CoffeeShop.Api/bin/Release/net10.0/CoffeeShop.Api.dll'
$apiDirectory = Split-Path -Parent $apiAssembly
$apiUrl = 'http://127.0.0.1:5234'
$database = 'corner_cafe_windows_demo'
$postgres = 'postgresql-x64-17'
New-Item -ItemType Directory -Path $results -Force | Out-Null

# The Windows 2025 hosted image includes PostgreSQL with the service disabled.
Set-Service -Name $postgres -StartupType Manual
Start-Service -Name $postgres
$env:PGPASSWORD = 'root'
try {
    & (Join-Path $env:PGBIN 'createdb.exe') -h 127.0.0.1 -U postgres $database
    $env:ConnectionStrings__CoffeeShop = "Host=127.0.0.1;Database=$database;Username=postgres;Password=$env:PGPASSWORD"
    $env:Jwt__Issuer = 'corner-cafe-e2e'
    $env:Jwt__Audience = 'corner-cafe-e2e'
    $env:Jwt__SigningKey = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    Write-Output "::add-mask::$env:Jwt__SigningKey"
    $env:Assistant__Provider = 'deterministic'
    $env:Logging__LogLevel__Default = 'Warning'
    $env:ProvisionStaff__Username = 'e2e_manager'
    $env:ProvisionStaff__Email = 'e2e-manager@example.test'
    $env:ProvisionStaff__FullName = 'E2E Manager'
    $env:ProvisionStaff__Roles = 'Manager'
    $env:ProvisionStaff__Password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    Write-Output "::add-mask::$env:ProvisionStaff__Password"
    dotnet $apiAssembly --migrate
    dotnet $apiAssembly --provision-staff
    & (Join-Path $env:PGBIN 'psql.exe') -X -h 127.0.0.1 -U postgres -d $database -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'postgres/windows-demo-seed.sql')

    $env:COFFEESHOP_CLIENT_MODE = 'api'
    $env:COFFEESHOP_API_BASE_URL = "$apiUrl/api/v1/"
    $env:COFFEESHOP_E2E_EXE = Join-Path $repository 'src/CoffeeShop.WinUI/bin/Release/net10.0-windows10.0.19041.0/win-x64/CoffeeShop.WinUI.exe'
    $env:COFFEESHOP_E2E_USERNAME = $env:ProvisionStaff__Username
    $env:COFFEESHOP_E2E_PASSWORD = $env:ProvisionStaff__Password

    $apiStart = @{
        FilePath = 'dotnet'
        ArgumentList = 'CoffeeShop.Api.dll', '--urls', $apiUrl
        WorkingDirectory = $apiDirectory
        WindowStyle = 'Hidden'
        PassThru = $true
        RedirectStandardOutput = Join-Path $results 'api.log'
        RedirectStandardError = Join-Path $results 'api-error.log'
    }
    $api = Start-Process @apiStart
    try {
        $ready = $false
        $deadline = [DateTime]::UtcNow.AddSeconds(40)
        do {
            try {
                $ready = (Invoke-WebRequest "$apiUrl/health/ready" -TimeoutSec 2).StatusCode -eq 200
            }
            catch {
                Start-Sleep -Seconds 1
            }
        } while (-not $ready -and -not $api.HasExited -and [DateTime]::UtcNow -lt $deadline)
        if (-not $ready) { throw 'The API did not become ready. See the API logs in the CI artifact.' }

        dotnet test (Join-Path $repository 'tests/CoffeeShop.WinUI.E2E.Tests') --no-build -c Release --logger 'trx;LogFileName=counter-sale.trx' --logger 'console;verbosity=normal' --results-directory $results
    }
    finally {
        if (-not $api.HasExited) { Stop-Process -InputObject $api }
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Stop-Service -Name $postgres
}
