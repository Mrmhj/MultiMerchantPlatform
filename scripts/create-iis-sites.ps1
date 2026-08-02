# =============================================================================
# MultiMerchantPlatform - IIS deployment script (21 backend + 4 frontend sites)
# Usage: run as Administrator:  powershell -ExecutionPolicy Bypass -File create-iis-sites.ps1
# Prereq: .NET 10 Hosting Bundle + URL Rewrite + ARR installed
# =============================================================================
$ErrorActionPreference = "Stop"

# ---------- Backend site definitions ----------
$backend = @(
    @{ Name = "mmp-gateway";      Port = 8000; Path = "E:\IISDeploy\services\gateway";            Dll = "ApiGateway.dll" },
    @{ Name = "mmp-identity";     Port = 8001; Path = "E:\IISDeploy\services\identity-service";   Dll = "IdentityService.dll" },
    @{ Name = "mmp-merchant";     Port = 8002; Path = "E:\IISDeploy\services\merchant-service";   Dll = "MerchantService.dll" },
    @{ Name = "mmp-product";      Port = 8003; Path = "E:\IISDeploy\services\product-service";    Dll = "ProductService.dll" },
    @{ Name = "mmp-order";        Port = 8004; Path = "E:\IISDeploy\services\order-service";      Dll = "OrderService.dll" },
    @{ Name = "mmp-pay";          Port = 8005; Path = "E:\IISDeploy\services\pay-service";        Dll = "PayService.dll" },
    @{ Name = "mmp-stock";        Port = 8006; Path = "E:\IISDeploy\services\stock-service";      Dll = "StockService.dll" },
    @{ Name = "mmp-cart";         Port = 8007; Path = "E:\IISDeploy\services\cart-service";       Dll = "CartService.dll" },
    @{ Name = "mmp-search";       Port = 8008; Path = "E:\IISDeploy\services\search-service";     Dll = "SearchService.dll" },
    @{ Name = "mmp-promotion";    Port = 8009; Path = "E:\IISDeploy\services\promotion-service";  Dll = "PromotionService.dll" },
    @{ Name = "mmp-messaging";    Port = 8010; Path = "E:\IISDeploy\services\messaging-service";  Dll = "MessagingService.dll" },
    @{ Name = "mmp-logging";      Port = 8011; Path = "E:\IISDeploy\services\logging-service";    Dll = "LoggingService.dll" },
    @{ Name = "mmp-review";       Port = 8012; Path = "E:\IISDeploy\services\review-service";     Dll = "ReviewService.dll" },
    @{ Name = "mmp-logistics";    Port = 8013; Path = "E:\IISDeploy\services\logistics-service";  Dll = "LogisticsService.dll" },
    @{ Name = "mmp-settlement";   Port = 8014; Path = "E:\IISDeploy\services\settlement-service"; Dll = "SettlementService.dll" },
    @{ Name = "mmp-email";        Port = 8015; Path = "E:\IISDeploy\services\email-service";      Dll = "EmailService.dll" },
    @{ Name = "mmp-im";           Port = 8016; Path = "E:\IISDeploy\services\im-service";         Dll = "ImService.dll" },
    @{ Name = "mmp-performance";  Port = 8017; Path = "E:\IISDeploy\services\performance-service"; Dll = "PerformanceService.dll" },
    @{ Name = "mmp-risk";         Port = 8018; Path = "E:\IISDeploy\services\risk-service";       Dll = "RiskService.dll" },
    @{ Name = "mmp-notification"; Port = 8019; Path = "E:\IISDeploy\services\notification-service"; Dll = "NotificationService.dll" },
    @{ Name = "mmp-bi-admin";     Port = 8020; Path = "E:\IISDeploy\services\bi-admin-service";   Dll = "BiAdminService.dll" }
)

# ---------- Frontend site definitions ----------
$frontend = @(
    @{ Name = "mmp-web-customer"; Port = 5173; Path = "E:\IISDeploy\web\web-customer" },
    @{ Name = "mmp-web-merchant"; Port = 5174; Path = "E:\IISDeploy\web\web-merchant" },
    @{ Name = "mmp-mobile";       Port = 5175; Path = "E:\IISDeploy\web\mobile-app" },
    @{ Name = "mmp-web-admin";    Port = 5177; Path = "E:\IISDeploy\web\web-admin" }
)

$appcmd = "C:\Windows\System32\inetsrv\appcmd.exe"

# Enable ARR reverse proxy (required for frontend /api rewrite to gateway)
& $appcmd set config -section:system.webServer/proxy /enabled:"True" /commit:apphost | Out-Null
Write-Host "[OK] ARR proxy enabled" -ForegroundColor Green

# ---------- Create backend sites ----------
foreach ($svc in $backend) {
    $dll = Join-Path $svc.Path $svc.Dll
    if (-not (Test-Path $dll)) {
        Write-Host "[SKIP] $($svc.Name): missing $($svc.Dll) in $($svc.Path)" -ForegroundColor Yellow
        continue
    }

    $pool = $svc.Name
    & $appcmd delete apppool $pool 2>$null | Out-Null
    & $appcmd add apppool /name:$pool /managedRuntimeVersion:"" /managedPipelineMode:Integrated | Out-Null
    & $appcmd set apppool $pool /processModel.identityType:ApplicationPoolIdentity /startMode:AlwaysRunning | Out-Null
    & $appcmd set apppool $pool /recycling.periodicRestart.time:00:00:00 | Out-Null

    & $appcmd delete site $pool 2>$null | Out-Null
    & $appcmd add site /name:$pool /physicalPath:$($svc.Path) /bindings:"http/*:$($svc.Port):" | Out-Null
    & $appcmd set app "$pool/" /applicationPool:$pool | Out-Null

    Write-Host "[OK] $($svc.Name) -> http://localhost:$($svc.Port) ($($svc.Dll))" -ForegroundColor Green
}

# ---------- Create frontend sites (static + reverse proxy /api -> gateway) ----------
foreach ($site in $frontend) {
    if (-not (Test-Path $site.Path)) {
        Write-Host "[SKIP] $($site.Name): dir not found ($($site.Path))" -ForegroundColor Yellow
        continue
    }

    $pool = $site.Name
    & $appcmd delete apppool $pool 2>$null | Out-Null
    & $appcmd add apppool /name:$pool /managedRuntimeVersion:"" /managedPipelineMode:Integrated | Out-Null
    & $appcmd set apppool $pool /processModel.identityType:ApplicationPoolIdentity /startMode:AlwaysRunning | Out-Null
    & $appcmd set apppool $pool /recycling.periodicRestart.time:00:00:00 | Out-Null

    & $appcmd delete site $pool 2>$null | Out-Null
    & $appcmd add site /name:$pool /physicalPath:$($site.Path) /bindings:"http/*:$($site.Port):" | Out-Null
    & $appcmd set app "$pool/" /applicationPool:$pool | Out-Null

    $webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="ProxyApiToGateway" stopProcessing="true">
          <match url="^api/(.*)" />
          <action type="Rewrite" url="http://localhost:8000/api/{R:1}" />
        </rule>
        <rule name="ProxyHubToGateway" stopProcessing="true">
          <match url="^hub/(.*)" />
          <action type="Rewrite" url="http://localhost:8000/hub/{R:1}" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
"@
    Set-Content -Path (Join-Path $site.Path "web.config") -Value $webConfig -Encoding UTF8
    Write-Host "[OK] $($site.Name) -> http://localhost:$($site.Port)" -ForegroundColor Green
}

Write-Host ""
Write-Host "============ IIS sites created ============" -ForegroundColor Cyan
