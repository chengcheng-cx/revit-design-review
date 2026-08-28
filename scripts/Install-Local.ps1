param(
    [string]$Configuration = "Debug",
    [string]$RevitInstallDir = $env:REVIT_2026_INSTALL_DIR,
    [string]$DotnetPath = "dotnet"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($RevitInstallDir)) {
    throw "Set REVIT_2026_INSTALL_DIR or pass -RevitInstallDir."
}

$dotnet = Get-Command $DotnetPath -ErrorAction Stop
& $dotnet.Source build "$repoRoot\src\RevitDesignReview.Revit2026\RevitDesignReview.Revit2026.csproj" `
    --configuration $Configuration `
    "/p:RevitInstallDir=$RevitInstallDir"

$source = "$repoRoot\src\RevitDesignReview.Revit2026\bin\$Configuration\net8.0-windows\win-x64"
$addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2026"
$destination = Join-Path $addinsRoot "RevitDesignReview"
$destinationFull = [System.IO.Path]::GetFullPath($destination)
$expectedRoot = [System.IO.Path]::GetFullPath($addinsRoot + [System.IO.Path]::DirectorySeparatorChar)
if (-not $destinationFull.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    [System.IO.Path]::GetFileName($destinationFull) -ne "RevitDesignReview") {
    throw "Refusing to replace an unexpected add-in directory: $destinationFull"
}

if (Test-Path -LiteralPath $destinationFull) {
    Remove-Item -LiteralPath $destinationFull -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -Path "$source\*" -Destination $destination -Recurse -Force

$assemblyPath = Join-Path $destination "RevitDesignReview.Revit2026.dll"
$template = Get-Content "$repoRoot\installer\RevitDesignReview.addin.template" -Raw
$manifest = $template.Replace("{{ASSEMBLY_PATH}}", $assemblyPath)
Set-Content -Path (Join-Path $addinsRoot "RevitDesignReview.addin") -Value $manifest -Encoding UTF8

Write-Host "Installed Revit Design Review for Revit 2026. Restart Revit to load it."
