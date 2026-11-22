param(
    [Parameter(Mandatory)][ValidateSet('before', 'after')][string]$Stage,
    [Parameter(Mandatory)][string]$DeviceSerial,
    [string[]]$Profiles = @('p100', 'p200', 't100', 't200'),
    [string[]]$RoleNames = @('manager', 'admin', 'inventory', 'delivery'),
    [string]$ResultsDirectory = '.maestro/results/usability'
)
$ErrorActionPreference = 'Stop'
# Run from apps/staff-mobile, with Metro serving the recorded source revision.
# Synthetic credentials/fixture names use the same MAESTRO_* variables as the
# existing native suite. Keep raw output ignored: it contains typed credentials.
foreach ($taskCommand in @('adb', 'maestro')) { $null = Get-Command $taskCommand -ErrorAction Stop }
$taskProfileMap = @{
    p100 = @{ size = '1080x2400'; density = '480'; font = '1.0'; night = 'no' }
    p200 = @{ size = '1080x2400'; density = '480'; font = '2.0'; night = 'yes' }
    t100 = @{ size = '1600x2560'; density = '320'; font = '1.0'; night = 'yes' }
    t200 = @{ size = '1600x2560'; density = '320'; font = '2.0'; night = 'no' }
}
foreach ($taskProfileName in $Profiles) {
    if (!$taskProfileMap.ContainsKey($taskProfileName)) { throw "Unknown profile: $taskProfileName" }
}
$taskOriginalSize = (& adb -s $DeviceSerial shell wm size | Out-String).Trim()
$taskOriginalDensity = (& adb -s $DeviceSerial shell wm density | Out-String).Trim()
$taskOriginalFont = (& adb -s $DeviceSerial shell settings get system font_scale | Out-String).Trim()
$taskOriginalNight = (& adb -s $DeviceSerial shell cmd uimode night | Out-String).Trim()
$taskRoles = @(
    @{ name = 'manager'; role = 'Manager'; flow = $(if ($Stage -eq 'before') { 'before' } else { 'manager' }) },
    @{ name = 'admin'; role = 'Admin'; flow = 'admin' },
    @{ name = 'inventory'; role = 'InventoryStaff'; flow = 'inventory' },
    @{ name = 'delivery'; role = 'DeliveryStaff'; flow = 'delivery' }
)
$taskRoles = @($taskRoles | Where-Object { $_.name -in $RoleNames })
if (!$taskRoles.Count) { throw 'Select at least one known audit role.' }
$taskRoot = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$taskCases = @()
$taskSummaryPath = Join-Path $taskRoot "summary-$Stage.json"
try {
    foreach ($taskProfileName in $Profiles) {
        $taskProfile = $taskProfileMap[$taskProfileName]
        # Apply device configuration before mounting the development app. Live
        # Activity recreation can leave two temporary Expo navigation roots.
        & adb -s $DeviceSerial shell am force-stop com.coffeeshop.staffmobile | Out-Null
        & adb -s $DeviceSerial shell wm size $taskProfile.size | Out-Null
        & adb -s $DeviceSerial shell wm density $taskProfile.density | Out-Null
        & adb -s $DeviceSerial shell settings put system font_scale $taskProfile.font | Out-Null
        & adb -s $DeviceSerial shell cmd uimode night $taskProfile.night | Out-Null
        foreach ($taskRole in $taskRoles) {
            $env:MAESTRO_STAFF_USERNAME = [Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_$($taskRole.role)_USERNAME")
            if (!$env:MAESTRO_STAFF_USERNAME -or !$env:MAESTRO_STAFF_PASSWORD) { throw 'Set synthetic role accounts and MAESTRO_STAFF_PASSWORD.' }
            $env:MAESTRO_UX_AFTER = if ($Stage -eq 'after') { 'true' } else { 'false' }
            $env:MAESTRO_CAPTURE_DIR = '.'
            $taskCaseDir = Join-Path $taskRoot "$Stage/$taskProfileName/$($taskRole.name)"
            New-Item -ItemType Directory -Path $taskCaseDir -Force | Out-Null
            $taskXmlPath = Join-Path $taskCaseDir 'junit.xml'
            $taskSavedPreference = $ErrorActionPreference
            try {
                # Native stderr must be retained even when parsing fails, so the
                # case is counted as failed instead of bypassing the report.
                $ErrorActionPreference = 'Continue'
                & maestro --device $DeviceSerial test --format junit --output $taskXmlPath --test-output-dir $taskCaseDir ".maestro/usability/$($taskRole.flow).yaml" *> (Join-Path $taskCaseDir 'private.log')
                $taskExit = $LASTEXITCODE
            } finally { $ErrorActionPreference = $taskSavedPreference }
            $taskNodes = @()
            if (Test-Path -LiteralPath $taskXmlPath) {
                [xml]$taskXml = Get-Content -Raw -LiteralPath $taskXmlPath
                $taskNodes = @($taskXml.SelectNodes('//testcase'))
            }
            $taskPassing = @($taskNodes | Where-Object { !$_.SelectSingleNode('failure') -and !$_.SelectSingleNode('error') -and !$_.SelectSingleNode('skipped') }).Count
            $taskOk = $taskExit -eq 0 -and $taskNodes.Count -eq 1 -and $taskPassing -eq 1
            $taskCases += @{ profile = $taskProfileName; role = $taskRole.name; passed = $taskOk; exitCode = $taskExit; cases = $taskNodes.Count; passing = $taskPassing }
            if ($taskOk) {
                $taskSavedPreference = $ErrorActionPreference
                try {
                    # adb writes ordinary transfer progress to stderr on Windows.
                    $ErrorActionPreference = 'Continue'
                    & adb -s $DeviceSerial shell uiautomator dump /sdcard/ux219-audit-tree.xml *> (Join-Path $taskCaseDir 'tree-dump.log')
                    if ($LASTEXITCODE) { throw 'Native hierarchy capture failed.' }
                    & adb -s $DeviceSerial pull /sdcard/ux219-audit-tree.xml (Join-Path $taskCaseDir 'tree.xml') *> (Join-Path $taskCaseDir 'tree-pull.log')
                    if ($LASTEXITCODE) { throw 'Native hierarchy transfer failed.' }
                } finally { $ErrorActionPreference = $taskSavedPreference }
            }
            Write-Output "$Stage/$taskProfileName/$($taskRole.name): $taskOk"
            if (!$taskOk -and $Stage -eq 'after') { throw 'Native audit failed. Inspect the private case log and retain the failed attempt.' }
        }
    }
} finally {
    & adb -s $DeviceSerial shell am force-stop com.coffeeshop.staffmobile | Out-Null
    if ($taskOriginalSize -match 'Override size: (\d+x\d+)') { & adb -s $DeviceSerial shell wm size $Matches[1] | Out-Null }
    else { & adb -s $DeviceSerial shell wm size reset | Out-Null }
    if ($taskOriginalDensity -match 'Override density: (\d+)') { & adb -s $DeviceSerial shell wm density $Matches[1] | Out-Null }
    else { & adb -s $DeviceSerial shell wm density reset | Out-Null }
    & adb -s $DeviceSerial shell settings put system font_scale $taskOriginalFont | Out-Null
    if ($taskOriginalNight -match 'Night mode: (\w+)') { & adb -s $DeviceSerial shell cmd uimode night $Matches[1] | Out-Null }
    @{ stage = $Stage; expected = $Profiles.Count * $taskRoles.Count; passed = @($taskCases | Where-Object passed).Count; cases = $taskCases; profiles = $taskProfileMap } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskSummaryPath
}
