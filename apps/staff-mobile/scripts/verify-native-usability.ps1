param(
    [Parameter(Mandatory)][ValidateSet('before', 'after')][string]$Stage,
    [Parameter(Mandatory)][string]$DeviceSerial,
    [string[]]$Profiles = @('p100', 'p200', 't100', 't200'),
    [string[]]$RoleNames = @('manager', 'admin', 'inventory', 'delivery'),
    [ValidateSet('queue', 'detail')][string]$ReferencePhase = 'queue',
    [string]$ResultsDirectory = '.maestro/results/usability',
    [string]$AppId = 'com.coffeeshop.staffmobile'
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
    # Measure the device's actual system bars below; do not assume their height.
    ref100 = @{ size = '1170x2664'; density = '480'; font = '1.0'; night = 'no' }
}
function Get-ReferenceBars([int]$ExpectedHeightPx) {
    $taskPreviousFrame = ''
    for ($taskAttempt = 0; $taskAttempt -lt 12; $taskAttempt++) {
        $taskWindowState = & adb -s $DeviceSerial shell dumpsys window displays | Out-String
        $taskBars = @{}
        foreach ($taskBarName in @('statusBars', 'navigationBars')) {
            $taskMatch = [regex]::Match($taskWindowState, "type=$taskBarName frame=\[(\d+),(\d+)\]\[(\d+),(\d+)\] visible=true")
            if (!$taskMatch.Success -or ([int]$taskMatch.Groups[3].Value - [int]$taskMatch.Groups[1].Value) -ne 1170) { break }
            $taskBars[$taskBarName] = [int]$taskMatch.Groups[4].Value - [int]$taskMatch.Groups[2].Value
            $taskBars.widthPx = 1170
            if ($taskBarName -eq 'navigationBars') { $taskBars.heightPx = [int]$taskMatch.Groups[4].Value }
        }
        if ($taskBars.ContainsKey('statusBars') -and $taskBars.ContainsKey('navigationBars') -and $taskBars.heightPx -eq $ExpectedHeightPx) {
            $taskFrame = "$($taskBars.statusBars)/$($taskBars.navigationBars)/$($taskBars.heightPx)"
            if ($taskFrame -eq $taskPreviousFrame) { return $taskBars }
            $taskPreviousFrame = $taskFrame
        }
        Start-Sleep -Milliseconds 250
    }
    throw 'Native reference display frames did not stabilize at the requested size.'
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
    @{ name = 'delivery'; role = 'DeliveryStaff'; flow = 'delivery' },
    @{ name = 'concept'; role = 'Manager'; flow = 'concept' }
    @{ name = 'mockup'; role = 'Manager'; flow = 'native-mockup' }
)
$taskRoles = @($taskRoles | Where-Object { $_.name -in $RoleNames })
if (!$taskRoles.Count) { throw 'Select at least one known audit role.' }
$taskRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ResultsDirectory)
New-Item -ItemType Directory -Path $taskRoot -Force | Out-Null
$taskCases = @()
$taskSummaryPath = Join-Path $taskRoot "summary-$Stage.json"
try {
    foreach ($taskProfileName in $Profiles) {
        $taskProfile = $taskProfileMap[$taskProfileName]
        # Apply device configuration before mounting the development app. Live
        # Activity recreation can leave two temporary Expo navigation roots.
        & adb -s $DeviceSerial shell am force-stop $AppId | Out-Null
        & adb -s $DeviceSerial shell wm size $taskProfile.size | Out-Null
        & adb -s $DeviceSerial shell wm density $taskProfile.density | Out-Null
        & adb -s $DeviceSerial shell settings put system font_scale $taskProfile.font | Out-Null
        & adb -s $DeviceSerial shell cmd uimode night $taskProfile.night | Out-Null
        if ($taskProfileName -eq 'ref100') {
            $taskBars = Get-ReferenceBars ([int]$taskProfile.size.Split('x')[1])
            $taskReferenceHeight = 844 * 3 + $taskBars.statusBars + $taskBars.navigationBars
            $taskProfile.size = "1170x$taskReferenceHeight"
            & adb -s $DeviceSerial shell wm size $taskProfile.size | Out-Null
        }
        foreach ($taskRole in $taskRoles) {
            $env:MAESTRO_STAFF_USERNAME = [Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_$($taskRole.role)_USERNAME")
            if (!$env:MAESTRO_STAFF_USERNAME -or !$env:MAESTRO_STAFF_PASSWORD) { throw 'Set synthetic role accounts and MAESTRO_STAFF_PASSWORD.' }
            $env:MAESTRO_UX_AFTER = if ($Stage -eq 'after') { 'true' } else { 'false' }
            $env:MAESTRO_APP_ID = $AppId
            $env:MAESTRO_CONCEPT_PHASE = $ReferencePhase
            $taskCaseDir = Join-Path $taskRoot "$Stage/$taskProfileName/$($taskRole.name)"
            New-Item -ItemType Directory -Path $taskCaseDir -Force | Out-Null
            # Maestro confines relative screenshot names to its per-run output
            # directory. Absolute paths are rejected by the CLI's path guard.
            $env:MAESTRO_CAPTURE_DIR = '.'
            $taskXmlPath = Join-Path $taskCaseDir 'junit.xml'
            $taskSavedPreference = $ErrorActionPreference
            try {
                # Native stderr must be retained even when parsing fails, so the
                # case is counted as failed instead of bypassing the report.
                $ErrorActionPreference = 'Continue'
                $taskFlowPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(".maestro/usability/$($taskRole.flow).yaml")
                & maestro --device $DeviceSerial test --format junit --output $taskXmlPath --test-output-dir $taskCaseDir $taskFlowPath *> (Join-Path $taskCaseDir 'private.log')
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
              try {
                $taskSavedPreference = $ErrorActionPreference
                try {
                    # adb writes ordinary transfer progress to stderr on Windows.
                    $ErrorActionPreference = 'Continue'
                    & adb -s $DeviceSerial shell uiautomator dump /sdcard/ux219-audit-tree.xml *> (Join-Path $taskCaseDir 'tree-dump.log')
                    if ($LASTEXITCODE) { throw 'Native hierarchy capture failed.' }
                    & adb -s $DeviceSerial pull /sdcard/ux219-audit-tree.xml (Join-Path $taskCaseDir 'tree.xml') *> (Join-Path $taskCaseDir 'tree-pull.log')
                    if ($LASTEXITCODE) { throw 'Native hierarchy transfer failed.' }
                } finally { $ErrorActionPreference = $taskSavedPreference }
                if ($taskProfileName -eq 'ref100') {
                    $taskBars = Get-ReferenceBars ([int]$taskProfile.size.Split('x')[1])
                    $taskContentHeight = ($taskBars.heightPx - $taskBars.statusBars - $taskBars.navigationBars) / 3
                    @{ sizePx = "$($taskBars.widthPx)x$($taskBars.heightPx)"; densityDpi = 480; topInsetPx = $taskBars.statusBars; bottomInsetPx = $taskBars.navigationBars; contentWidthDp = $taskBars.widthPx / 3; contentHeightDp = $taskContentHeight } | ConvertTo-Json | Set-Content (Join-Path $taskCaseDir 'reference-geometry.json')
                    if ($taskContentHeight -ne 844) {
                        $taskCases[-1].passed = $false
                        throw 'The measured reference content viewport is not 390x844dp.'
                    }
                }
              } catch {
                $taskCases[-1].passed = $false
                $taskCases[-1].artifactError = 'Native artifact or reference geometry validation failed.'
                throw
              }
            }
            Write-Output "$Stage/$taskProfileName/$($taskRole.name): $taskOk"
            if (!$taskOk -and $Stage -eq 'after') { throw 'Native audit failed. Inspect the private case log and retain the failed attempt.' }
        }
    }
} finally {
    & adb -s $DeviceSerial shell am force-stop $AppId | Out-Null
    if ($taskOriginalSize -match 'Override size: (\d+x\d+)') { & adb -s $DeviceSerial shell wm size $Matches[1] | Out-Null }
    else { & adb -s $DeviceSerial shell wm size reset | Out-Null }
    if ($taskOriginalDensity -match 'Override density: (\d+)') { & adb -s $DeviceSerial shell wm density $Matches[1] | Out-Null }
    else { & adb -s $DeviceSerial shell wm density reset | Out-Null }
    & adb -s $DeviceSerial shell settings put system font_scale $taskOriginalFont | Out-Null
    if ($taskOriginalNight -match 'Night mode: (\w+)') { & adb -s $DeviceSerial shell cmd uimode night $Matches[1] | Out-Null }
    @{ stage = $Stage; expected = $Profiles.Count * $taskRoles.Count; passed = @($taskCases | Where-Object passed).Count; cases = $taskCases; profiles = $taskProfileMap } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $taskSummaryPath
}
