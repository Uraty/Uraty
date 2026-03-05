param(
  [Parameter(Mandatory=$true)][string]$TargetDir,
  [Parameter(Mandatory=$true)][string]$AssemblyName,
  [Parameter(Mandatory=$true)][string]$UnityAnalyzersDir
)

$ErrorActionPreference = "Stop"

$targetDirFull = [System.IO.Path]::GetFullPath($TargetDir)
$unityDirFull  = [System.IO.Path]::GetFullPath($UnityAnalyzersDir)

$depsPath = Join-Path $targetDirFull "$AssemblyName.deps.json"
$dllPath  = Join-Path $targetDirFull "$AssemblyName.dll"

if (!(Test-Path $depsPath)) { throw "deps.json not found: $depsPath" }
if (!(Test-Path $dllPath))  { throw "Analyzer dll not found: $dllPath" }

# NuGet global-packages path
$nugetRootLine = (dotnet nuget locals global-packages -l | Select-String "global-packages:").Line
$nugetRoot = $nugetRootLine.Split(":",2)[1].Trim()
if (!(Test-Path $nugetRoot)) { throw "NuGet cache not found: $nugetRoot" }

# Read deps.json
$json = Get-Content $depsPath -Raw | ConvertFrom-Json

# pick target framework key that ends with "/"
$targetKey = ($json.targets.PSObject.Properties.Name | Where-Object { $_.EndsWith("/") } | Select-Object -First 1)
if ([string]::IsNullOrWhiteSpace($targetKey)) { throw "Target key not found in deps.json" }

$target = $json.targets.$targetKey
$libraries = $json.libraries

New-Item -ItemType Directory -Force -Path $unityDirFull | Out-Null

# Clean old dlls (keep .meta)
Get-ChildItem -Path $unityDirFull -Filter *.dll -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# Copy Analyzer.dll itself
Copy-Item -Force $dllPath (Join-Path $unityDirFull "$AssemblyName.dll")

# Copy runtime dlls from NuGet cache based on deps.json
$copied = New-Object System.Collections.Generic.HashSet[string]

foreach ($libProp in $target.PSObject.Properties) {
  $libName = $libProp.Name
  $lib = $libProp.Value

  if ($null -eq $lib.runtime) { continue }

  $libInfo = $libraries.$libName
  if ($null -eq $libInfo) { continue }
  if ($libInfo.type -ne "package") { continue }
  if ([string]::IsNullOrWhiteSpace($libInfo.path)) { continue }

  foreach ($rt in $lib.runtime.PSObject.Properties) {
    $rel = $rt.Name
    if (-not $rel.EndsWith(".dll")) { continue }
    if ($rel -match "\.resources\.dll$") { continue } # skip satellite resource dlls

    $relWin = $rel -replace "/", "\"
    $src = Join-Path $nugetRoot (Join-Path $libInfo.path $relWin)

    if (!(Test-Path $src)) {
      throw "Missing runtime dll referenced by deps.json: $src"
    }

    $dst = Join-Path $unityDirFull ([System.IO.Path]::GetFileName($src))
    if ($copied.Add($dst)) {
      Copy-Item -Force $src $dst
    }
  }
}

Write-Host "Copied Analyzer + dependencies to: $unityDirFull"