$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\..\src\CrestApps.AgentSkills\orchardcore'
$sourcePath = [System.IO.Path]::GetFullPath($sourcePath)

$destinationPath = Join-Path $PSScriptRoot 'skills'
$destinationPath = [System.IO.Path]::GetFullPath($destinationPath)

if (-not (Test-Path -Path $sourcePath -PathType Container)) {
    throw "Source skills directory not found: $sourcePath"
}

New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null

robocopy $sourcePath $destinationPath /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null

if ($LASTEXITCODE -gt 7) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}

Write-Host "Plugin skills synced from '$sourcePath' to '$destinationPath'."
