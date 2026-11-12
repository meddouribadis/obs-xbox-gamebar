# Build + (re)deploy le widget Xbox Game Bar en ligne de commande.
# Usage : powershell -ExecutionPolicy Bypass -File .\ObsGameBarWidget\Deploy-Widget.ps1 [-Configuration Debug] [-Platform x64]

param(
    [string]$Configuration = 'Debug',
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$projDir = $PSScriptRoot
$csproj  = Join-Path $projDir 'ObsGameBarWidget.csproj'
$outDir  = Join-Path $projDir "bin\$Platform\$Configuration"
$layout  = Join-Path $outDir 'AppX'

# 1. Localiser MSBuild (Visual Studio)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild introuvable (Visual Studio installe ?)" }

# 2. Fermer l'appli si elle tourne (fichiers verrouilles)
Get-Process ObsGameBarWidget -ErrorAction SilentlyContinue | Stop-Process -Force

# 3. Build
Write-Host "==> Build ($Configuration|$Platform)" -ForegroundColor Cyan
& $msbuild $csproj /restore "/p:Configuration=$Configuration" "/p:Platform=$Platform" /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Build en echec" }

# 4. Synchroniser le dossier de deploiement (AppX) avec la sortie du build
#    (MSBuild seul ne met pas ce dossier a jour, c'est Visual Studio qui le fait au F5)
Write-Host "==> Mise a jour du layout $layout" -ForegroundColor Cyan
[xml]$recipe = Get-Content (Join-Path $outDir 'ObsGameBarWidget.build.appxrecipe')
foreach ($f in $recipe.SelectNodes('//*[local-name()="AppxPackagedFile"]')) {
    $target = $f.PackagePath
    if ($target -and $target -ne 'AppxManifest.xml' -and (Test-Path $f.Include -PathType Leaf)) {
        $dst = Join-Path $layout $target
        New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
        Copy-Item $f.Include $dst -Force
    }
}

# 5. Manifeste : on garde le manifeste "neutral" comme Visual Studio
$newManifest = (Get-Content (Join-Path $outDir 'AppxManifest.xml') -Raw -Encoding UTF8) -replace ' ProcessorArchitecture="[^"]*"', ''
$layoutManifest = Join-Path $layout 'AppxManifest.xml'
$manifestChanged = (-not (Test-Path $layoutManifest)) -or ((Get-Content $layoutManifest -Raw -Encoding UTF8) -ne $newManifest)
if ($manifestChanged) {
    [IO.File]::WriteAllText($layoutManifest, $newManifest, (New-Object Text.UTF8Encoding $false))
}

# 6. Enregistrer le package (re-enregistrement obligatoire si le manifeste a change)
$installed = Get-AppxPackage OBSStudio.GameBarWidget
if ($manifestChanged -or -not $installed) {
    if ($installed) {
        Write-Host "==> Manifeste modifie : desinstallation de l'ancien package (les reglages sauvegardes seront effaces)" -ForegroundColor Yellow
        $installed | Remove-AppxPackage
    }
    Write-Host "==> Enregistrement du package" -ForegroundColor Cyan
    Add-AppxPackage -Register $layoutManifest
} else {
    Write-Host "==> Manifeste inchange : pas de re-enregistrement necessaire" -ForegroundColor Cyan
}

Write-Host "OK. Ferme puis rouvre le widget dans Game Bar (Win+G)." -ForegroundColor Green
