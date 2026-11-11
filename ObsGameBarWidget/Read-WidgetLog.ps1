# Script pour afficher les logs de débogage du widget Xbox Game Bar

$package = Get-AppxPackage *OBSStudio.GameBarWidget* -ErrorAction SilentlyContinue

if ($package) {
    $family = $package.PackageFamilyName
    $logPath = "$env:LOCALAPPDATA\Packages\$family\LocalState\widget_debug.log"
    
    if (Test-Path $logPath) {
        Write-Host "=== LOGS DU WIDGET ($logPath) ===" -ForegroundColor Green
        Get-Content $logPath
    } else {
        Write-Host "Le fichier de log n'existe pas encore à l'emplacement :" -ForegroundColor Yellow
        Write-Host $logPath -ForegroundColor Gray
        Write-Host "Assurez-vous d'avoir ouvert le widget au moins une fois après le redéploiement." -ForegroundColor Gray
    }
} else {
    Write-Host "Le package OBSStudio.GameBarWidget n'est pas installé." -ForegroundColor Red
}
