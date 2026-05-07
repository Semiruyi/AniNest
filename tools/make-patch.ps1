param(
    [Parameter(Mandatory = $true)]
    [string]$BaseAppDir,
    [Parameter(Mandatory = $true)]
    [string]$NewAppDir,
    [Parameter(Mandatory = $true)]
    [string]$OutputZip,
    [string]$BaseVersion = "",
    [string]$Version = "",
    [string]$Description = "patch"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-Sha256([string]$path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($path)
        try {
            $hashBytes = $sha.ComputeHash($stream)
        }
        finally {
            $stream.Dispose()
        }

        -join ($hashBytes | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $sha.Dispose()
    }
}

function Get-RelativeFiles([string]$root) {
    $normalizedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object {
        $fullName = [System.IO.Path]::GetFullPath($_.FullName)
        [PSCustomObject]@{
            FullName = $fullName
            RelativePath = $fullName.Substring($normalizedRoot.Length).Replace('\', '/')
            Length = $_.Length
        }
    }
}

$baseAppDir = (Resolve-Path $BaseAppDir).Path
$newAppDir = (Resolve-Path $NewAppDir).Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $newManifestPath = Join-Path $newAppDir "manifest.json"
    if (Test-Path $newManifestPath) {
        $Version = (Get-Content $newManifestPath -Raw | ConvertFrom-Json).version
    }
}

if ([string]::IsNullOrWhiteSpace($BaseVersion)) {
    $baseManifestPath = Join-Path $baseAppDir "manifest.json"
    if (Test-Path $baseManifestPath) {
        $BaseVersion = (Get-Content $baseManifestPath -Raw | ConvertFrom-Json).version
    }
}

if ([string]::IsNullOrWhiteSpace($Version) -or [string]::IsNullOrWhiteSpace($BaseVersion)) {
    throw "BaseVersion and Version are required."
}

$baseFiles = @{}
Get-RelativeFiles $baseAppDir | ForEach-Object { $baseFiles[$_.RelativePath] = $_ }

$newFiles = @{}
Get-RelativeFiles $newAppDir | ForEach-Object { $newFiles[$_.RelativePath] = $_ }

$entries = New-Object System.Collections.Generic.List[object]
$payloads = New-Object System.Collections.Generic.List[object]

foreach ($relativePath in $newFiles.Keys | Sort-Object) {
    if ($relativePath -eq "manifest.json") {
        continue
    }

    $newFile = $newFiles[$relativePath]
    $needsAdd = -not $baseFiles.ContainsKey($relativePath)
    $needsReplace = $false

    if (-not $needsAdd) {
        $baseFile = $baseFiles[$relativePath]
        if ($baseFile.Length -ne $newFile.Length) {
            $needsReplace = $true
        } elseif ((Get-Sha256 $baseFile.FullName) -ne (Get-Sha256 $newFile.FullName)) {
            $needsReplace = $true
        }
    }

    if ($needsAdd -or $needsReplace) {
        $entries.Add([ordered]@{
            path = $relativePath
            action = $(if ($needsAdd) { "add" } else { "replace" })
            sha256 = (Get-Sha256 $newFile.FullName)
            sizeBytes = $newFile.Length
        }) | Out-Null
        $payloads.Add($newFile) | Out-Null
    }
}

foreach ($relativePath in $baseFiles.Keys | Sort-Object) {
    if ($relativePath -eq "manifest.json") {
        continue
    }

    if (-not $newFiles.ContainsKey($relativePath)) {
        $entries.Add([ordered]@{
            path = $relativePath
            action = "delete"
            sha256 = $null
            sizeBytes = 0
        }) | Out-Null
    }
}

$manifest = [ordered]@{
    appId = "LocalPlayer"
    packageType = "patch"
    version = $Version
    baseVersion = $BaseVersion
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    description = $Description
    files = $entries
}

if (Test-Path $OutputZip) {
    Remove-Item -LiteralPath $OutputZip -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($OutputZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $manifestEntry = $zip.CreateEntry("manifest.json", [System.IO.Compression.CompressionLevel]::Optimal)
    $manifestWriter = New-Object IO.StreamWriter($manifestEntry.Open())
    try {
        $manifestWriter.Write(($manifest | ConvertTo-Json -Depth 8))
    }
    finally {
        $manifestWriter.Dispose()
    }

    foreach ($payload in $payloads) {
        $entry = $zip.CreateEntry($payload.RelativePath, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        try {
            $fileStream = [System.IO.File]::OpenRead($payload.FullName)
            try {
                $fileStream.CopyTo($entryStream)
            }
            finally {
                $fileStream.Dispose()
            }
        }
        finally {
            $entryStream.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Created patch: $OutputZip"
