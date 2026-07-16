# Nap du lieu mau (tai khoan + danh muc + san pham) vao MongoDB.
# Tu doc MONGO_CONNECTION trong .env -> nap dung dich (Atlas hoac Docker local).
# Cach dung:  .\seed-data.ps1
# (Script tieng Viet khong dau vi PowerShell 5.1 doc .ps1 theo ANSI)

$ErrorActionPreference = "Stop"
$seedFile = Join-Path $PSScriptRoot "seed\pizzaapp-seed.tgz"
$envPath  = Join-Path $PSScriptRoot ".env"

Write-Host "== Nap du lieu mau vao MongoDB ==" -ForegroundColor Cyan

if (-not (Test-Path $seedFile)) { Write-Host "LOI: khong thay $seedFile" -ForegroundColor Red; exit 1 }

# --- Doc dich den tu .env ---
$uri = "mongodb://localhost:27017"   # mac dinh: Docker local
$target = "Docker local"
if (Test-Path $envPath) {
    $line = (Get-Content $envPath | Where-Object { $_ -match "^MONGO_CONNECTION=" } | Select-Object -First 1)
    if ($line) {
        $uri = $line -replace "^MONGO_CONNECTION=", ""
        if ($uri -match "mongodb\+srv") { $target = "Atlas (cloud)" }
    }
}
Write-Host "  Dich den: $target" -ForegroundColor Yellow

# Container mongo dung lam 'may chay lenh' (co san mongorestore)
$mongo = docker ps --filter "name=backend-mongo-1" --format "{{.Names}}" 2>$null
if (-not $mongo) {
    Write-Host "LOI: container backend-mongo-1 chua chay (can no de co lenh mongorestore)." -ForegroundColor Red
    Write-Host "  -> Chay truoc:  docker compose up -d" -ForegroundColor Yellow
    exit 1
}

Write-Host "  Dang chep seed vao container..." -ForegroundColor Gray
docker cp $seedFile backend-mongo-1:/tmp/seed.tgz | Out-Null

Write-Host "  Dang nap (--drop: ghi de du lieu cu)..." -ForegroundColor Gray
$cmd = "rm -rf /tmp/s && mkdir -p /tmp/s && tar xzf /tmp/seed.tgz -C /tmp/s && mongorestore --uri='$uri' --drop /tmp/s 2>&1 | grep -E 'finished restoring|failures|error'"
docker exec backend-mongo-1 sh -c $cmd | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

# --- Kiem chung (goi thang mongosh, khong qua sh -c de tranh loi quote) ---
$js = "print('Users=' + db.getSiblingDB('PizzaApp_Auth').Users.countDocuments() + '  Categories=' + db.getSiblingDB('PizzaApp_Category').Categories.countDocuments() + '  Products=' + db.getSiblingDB('PizzaApp_Product').Products.countDocuments())"
$counts = (docker exec backend-mongo-1 mongosh $uri --quiet --eval $js) -join ' '

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host " XONG! ($target)  $counts" -ForegroundColor Green
Write-Host ""
Write-Host " Tai khoan Admin:  admin@pizza.com / admin123" -ForegroundColor White
Write-Host "=============================================" -ForegroundColor Green
