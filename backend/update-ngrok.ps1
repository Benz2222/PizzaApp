# Tu dong lay URL ngrok -> cap nhat .env -> restart payment.
# Cach dung: mo ngrok truoc (ngrok http 8080), roi chay:  .\update-ngrok.ps1
# (Script dung tieng Viet khong dau vi PowerShell 5.1 doc .ps1 theo ANSI)

Write-Host "== Dang lay URL ngrok ==" -ForegroundColor Cyan

try {
    $tunnels = Invoke-RestMethod -Uri "http://127.0.0.1:4040/api/tunnels" -TimeoutSec 5
} catch {
    Write-Host "LOI: Khong thay ngrok dang chay." -ForegroundColor Red
    Write-Host "  -> Mo cua so khac chay:  ngrok http 8080   roi chay lai script nay." -ForegroundColor Yellow
    exit 1
}

$url = ($tunnels.tunnels | Where-Object { $_.proto -eq "https" } | Select-Object -First 1).public_url
if (-not $url) {
    Write-Host "LOI: ngrok chay nhung khong co tunnel https." -ForegroundColor Red
    exit 1
}
Write-Host "  URL ngrok: $url" -ForegroundColor Green

# --- Cap nhat .env ---
$envPath = Join-Path $PSScriptRoot ".env"
if (-not (Test-Path $envPath)) {
    Write-Host "LOI: khong thay $envPath" -ForegroundColor Red
    exit 1
}

$lines = Get-Content $envPath
$new = [ordered]@{
    "PUBLIC_BASE_URL"  = $url
    "PAYOS_RETURN_URL" = "$url/api/payment/return"
    "PAYOS_CANCEL_URL" = "$url/api/payment/return"
}

foreach ($key in $new.Keys) {
    $value = $new[$key]
    if ($lines -match "^$key=") {
        $lines = $lines -replace "^$key=.*", "$key=$value"
    } else {
        $lines += "$key=$value"
    }
}
$lines | Set-Content $envPath -Encoding ascii
Write-Host "  Da cap nhat .env (3 bien)" -ForegroundColor Green

# --- Restart payment de nap bien moi (service tu dang ky webhook voi PayOS) ---
Write-Host "== Restart service payment ==" -ForegroundColor Cyan
docker compose up -d payment | Out-Null

Write-Host "== Cho payment tu dang ky webhook voi PayOS ==" -ForegroundColor Cyan
# Chi match phan ASCII cua log (log app co dau tieng Viet)
$ok = $false
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 3
    $log = (docker logs backend-payment-1 2>&1 | Out-String)
    if ($log -match "webhook PayOS: http") { $ok = $true; break }
    if ($log -match "my\.payos\.vn") { break }
}

Write-Host ""
if ($ok) {
    Write-Host "=============================================================" -ForegroundColor Green
    Write-Host " XONG! Webhook da tu dong dang ky voi PayOS." -ForegroundColor Green
    Write-Host " Khong can vao my.payos.vn. Cu dat hang va quet QR." -ForegroundColor Green
    Write-Host "=============================================================" -ForegroundColor Green
} else {
    Write-Host "=============================================================" -ForegroundColor Yellow
    Write-Host " Tu dang ky webhook THAT BAI -> khai tay tai my.payos.vn:" -ForegroundColor Yellow
    Write-Host "   $url/api/payment/webhook" -ForegroundColor White
    Write-Host " Xem log:  docker logs backend-payment-1 | findstr Payment" -ForegroundColor Yellow
    Write-Host "=============================================================" -ForegroundColor Yellow
}
