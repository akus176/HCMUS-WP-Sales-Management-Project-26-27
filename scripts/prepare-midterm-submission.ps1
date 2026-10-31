[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]{8}$')]
    [string]$StudentId,

    [Parameter(Mandatory = $true)]
    [string]$YouTubeUrl,

    [Parameter(Mandatory = $true)]
    [string]$TokenFile,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repositoryPrefix = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\') + '\'

$video = $null
if (-not [Uri]::TryCreate($YouTubeUrl, [UriKind]::Absolute, [ref]$video) -or
    $video.Scheme -ne 'https' -or
    $video.Host -notin @('youtube.com', 'www.youtube.com', 'youtu.be', 'www.youtu.be')) {
    throw 'Provide the HTTPS URL of the unlisted YouTube demonstration.'
}

$resolvedToken = Resolve-Path -LiteralPath $TokenFile
$tokenPath = [IO.Path]::GetFullPath($resolvedToken.Path)
if ($tokenPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep pat.txt outside the repository. Supply the path to that external file.'
}
if ((Get-Item -LiteralPath $tokenPath).Length -eq 0) {
    throw 'The token file is empty.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $repositoryRoot) 'midterm-submission'
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (($outputPath.TrimEnd('\') + '\').StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The submission archive must be written outside the repository.'
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$archivePath = Join-Path $outputPath "$StudentId.zip"
if (Test-Path -LiteralPath $archivePath) {
    throw "The archive already exists: $archivePath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-TextEntry {
    param([IO.Compression.ZipArchive]$Archive, [string]$Name, [string]$Value)
    $entry = $Archive.CreateEntry($Name)
    $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($Value.TrimEnd() + "`n") }
    finally { $writer.Dispose() }
}

$archive = $null
$complete = $false
try {
    $archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
    Add-TextEntry $archive 'repo.txt' 'https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27'
    Add-TextEntry $archive 'youtube.txt' $video.AbsoluteUri
    foreach ($name in @('instructions.txt', 'info.txt')) {
        $source = Join-Path $repositoryRoot "docs/midterm-submission/$name"
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $name) | Out-Null
    }
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $tokenPath, 'pat.txt') | Out-Null
    $complete = $true
}
finally {
    if ($null -ne $archive) { $archive.Dispose() }
    if (-not $complete -and (Test-Path -LiteralPath $archivePath)) {
        Remove-Item -LiteralPath $archivePath
    }
}

Write-Output "Created $archivePath with repo.txt, pat.txt, youtube.txt, instructions.txt, and info.txt."
Write-Output 'Confirm the YouTube video is Unlisted and the read-only repository token remains valid through grading.'
