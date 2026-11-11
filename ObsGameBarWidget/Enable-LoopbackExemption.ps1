# Script pour débloquer la communication réseau locale (localhost / 127.0.0.1)
# pour le widget Xbox Game Bar sous Windows (UWP AppContainer Loopback Exemption).

Write-Host "Recherche du package OBS Game Bar Widget..." -ForegroundColor Cyan

$package = Get-AppxPackage *OBSStudio.GameBarWidget* -ErrorAction SilentlyContinue

if ($package) {
    $family = $package.PackageFamilyName
    Write-Host "Package trouvé : $family" -ForegroundColor Green
    Write-Host "Application de l'exemption Loopback via CheckNetIsolation..." -ForegroundColor Yellow
    
    $result = CheckNetIsolation.exe LoopbackExempt -a "-n=$family"
    Write-Host $result
    Write-Host "Succès ! Le widget peut désormais communiquer avec OBS sur localhost:4455." -ForegroundColor Green
} else {
    Write-Host "Le package n'est pas encore déployé sur votre système." -ForegroundColor Yellow
    Write-Host "Déployez ou lancez d'abord le projet depuis Visual Studio (F5 ou Déployer la solution)," -ForegroundColor Gray
    Write-Host "puis réexécutez ce script." -ForegroundColor Gray
}
