# RUNBOOK — Chạy & tắt hệ thống PizzaApp Microservice

Hệ thống gồm: **9 container Docker** (6 microservice + gateway + MongoDB + RabbitMQ) + **emulator Android** + **app Flutter**.

---

## ⚡ TL;DR — Chạy nhanh (chế độ Mock, không tiền thật)

```powershell
# 1. Mở Docker Desktop (Start Menu), đợi "Engine running"

# 2. Backend
cd D:\PizzaApp\backend
docker compose up -d

# 3. Emulator (giữ cửa sổ này mở)
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd Pixel_4 -gpu host

# 4. Mở app (cửa sổ PowerShell khác)
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell monkey -p com.example.pizza_app_flutter -c android.intent.category.LAUNCHER 1
```

---

# 🟢 KHỞI ĐỘNG — chi tiết

## Bước 1. Docker Desktop
- Mở **Docker Desktop** từ Start Menu.
- Đợi góc dưới-trái hiện **"Engine running"** (cá voi 🐳 xanh). Mất ~30–60 giây.
- Kiểm tra: `docker ps` → không báo lỗi là OK.

## Bước 2. Chọn chế độ thanh toán

### 🅐 Chế độ **Mock** (giả lập — KHÔNG tiền thật) — dùng để demo/tập
Sửa `D:\PizzaApp\backend\.env`:
```env
PAYMENT_PROVIDER=Mock
PUBLIC_BASE_URL=http://10.0.2.2:8090
```
> `10.0.2.2` = địa chỉ máy host nhìn từ Android emulator. **Không dùng `localhost`** (emulator hiểu localhost là chính nó).
> Nếu demo trên **điện thoại thật**: dùng IP LAN, vd `http://192.168.1.132:8090`.

**Không cần ngrok.** Bỏ qua bước 3, sang bước 4.

### 🅑 Chế độ **PayOS** (TIỀN THẬT — quét QR ngân hàng)
Sửa `.env`:
```env
PAYMENT_PROVIDER=PayOS
```
→ **Bắt buộc làm bước 3 (ngrok)**, vì PayOS phải gọi ngược vào server bạn từ internet.

## Bước 3. ngrok (CHỈ khi dùng PayOS)

**3a. Mở tunnel** — cửa sổ PowerShell riêng, **giữ mở suốt buổi demo**:
```powershell
ngrok http 8090
```

**3b. Cập nhật cấu hình — CHẠY SCRIPT, KHÔNG SỬA TAY**

Cửa sổ PowerShell khác:
```powershell
cd D:\PizzaApp\backend
.\update-ngrok.ps1
```

Script tự làm **hết**, bạn không phải động vào gì:
1. Lấy URL ngrok hiện tại
2. Ghi vào `.env` 3 biến: `PUBLIC_BASE_URL`, `PAYOS_RETURN_URL`, `PAYOS_CANCEL_URL`
3. Restart service `payment`
4. Payment service **tự gọi API PayOS đăng ký webhook** (`payOS.confirmWebhook`)
5. Script chờ và báo `XONG! Webhook da tu dong dang ky voi PayOS.`

✅ **KHÔNG cần vào my.payos.vn.** Thấy dòng "XONG!" là dùng được ngay.

> Nếu báo `cannot be loaded because running scripts is disabled`, chạy:
> `powershell -ExecutionPolicy Bypass -File .\update-ngrok.ps1`

> ⚠️ **ngrok free đổi URL mỗi lần chạy lại** → mỗi lần mở ngrok mới, chỉ cần chạy lại `.\update-ngrok.ps1`.

**Nếu script báo đăng ký webhook THẤT BẠI** → mới phải khai tay tại **my.payos.vn → Webhook**, dán URL script in ra. Xem lý do:
```powershell
docker logs backend-payment-1 | findstr Payment
```

### 3 biến đó để làm gì?
| Biến | Ý nghĩa |
|---|---|
| `PUBLIC_BASE_URL` | Địa chỉ công khai của hệ thống (thay cho localhost) |
| `PAYOS_RETURN_URL` | Trả tiền xong PayOS đưa người dùng về đây → trang này tự mở lại app qua deep link |
| `PAYOS_CANCEL_URL` | Nơi quay về khi bấm huỷ thanh toán |

## Bước 4. Backend (9 container)

```powershell
cd D:\PizzaApp\backend
docker compose up -d
```

**Kiểm tra**:
```powershell
docker compose ps                                  # phải thấy 9 container "Up"
curl http://localhost:8090/api/category            # phải trả JSON danh mục
docker logs backend-payment-1 | Select-String "Payment]"   # xác nhận Mock hay PayOS
```

### Có bắt buộc chạy `docker compose up -d` không?
**Tuỳ cách tắt lần trước:**

| Lần trước tắt bằng | Mở Docker Desktop lên | Cần `up -d`? |
|---|---|---|
| `docker compose down` | Container bị **xoá** | ✅ **Bắt buộc** |
| `docker compose stop` | Container **đang dừng** | ✅ Cần |
| Chỉ Quit Docker Desktop | Docker **tự khôi phục** container | ❌ Không cần |

👉 **Cứ chạy `docker compose up -d` mỗi lần cho chắc** — lệnh này idempotent, đang chạy rồi thì nó chỉ in `Running` chứ không phá gì.

⚠️ **BẮT BUỘC chạy `up -d` sau khi sửa `.env` hoặc `docker-compose.yml`** — không chạy thì container vẫn dùng cấu hình cũ.
> Sửa mỗi biến của payment → `docker compose up -d payment` là đủ.

### ⚠️ BẪY LỚN: sửa code C# thì phải thêm `--build`
```powershell
docker compose up -d --build          # sau khi SỬA CODE backend
docker compose up -d --build payment  # chỉ build lại 1 service
```
Sửa code C# mà chỉ chạy `up -d` → Docker thấy container đang chạy → **không làm gì** → **code mới KHÔNG được áp dụng**, ngồi thắc mắc "sao sửa rồi mà không đổi".

| Sửa gì | Lệnh |
|---|---|
| `.env` / `docker-compose.yml` | `docker compose up -d` |
| **Code C# backend** | `docker compose up -d --build` |
| Code Flutter | `flutter run --no-enable-impeller` (build lại APK) |

## Bước 5. Emulator

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd Pixel_4 -gpu host
```
- **Giữ cửa sổ PowerShell này mở** (đóng = emulator tắt).
- `-gpu host` = dùng GPU thật → mượt. **Đừng dùng** `-gpu swiftshader_indirect` (render bằng CPU → đứng máy).
- Đợi vào màn hình home Android (~1 phút).

## Bước 6. Mở app

### Trường hợp thường — KHÔNG sửa code (chỉ demo)
APK đã cài sẵn, mở thẳng, **không cần `flutter run`**:
```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell monkey -p com.example.pizza_app_flutter -c android.intent.category.LAUNCHER 1
```
✅ Cách này **tiết kiệm ~900MB RAM** (không chạy Gradle daemon) → app không bị "Lost connection".

### Trường hợp CÓ sửa code Flutter → phải build lại
```powershell
cd D:\PizzaApp\pizza_flutter
flutter run --no-enable-impeller
```
- **`--no-enable-impeller` là BẮT BUỘC.** Không có nó → app trắng/không hiện (lỗi renderer Impeller trên emulator).
- App lên rồi → bấm **`q`** để thoát flutter run → rồi dọn RAM:
  ```powershell
  taskkill /F /IM java.exe
  ```
- Mở lại app bằng lệnh `adb monkey` ở trên.

---

# 🔴 TẮT — chi tiết

Tắt theo thứ tự này:

```powershell
# 1. Tắt emulator
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" emu kill
#    (hoặc đóng cửa sổ emulator / Ctrl+C ở cửa sổ chạy emulator)

# 2. Tắt backend — GIỮ dữ liệu
cd D:\PizzaApp\backend
docker compose down

# 3. Tắt ngrok: bấm Ctrl+C ở cửa sổ ngrok

# 4. Dọn tiến trình thừa
taskkill /F /IM java.exe      # Gradle daemon
taskkill /F /IM dart.exe      # Flutter daemon

# 5. Thoát Docker Desktop: chuột phải icon 🐳 khay hệ thống -> Quit Docker Desktop
```

> ⚠️ **`docker compose down -v` sẽ XÓA SẠCH database** (users, sản phẩm, đơn hàng). Chỉ dùng khi muốn làm lại từ đầu.
> Dùng `docker compose down` (không `-v`) → dữ liệu vẫn còn trong volume `mongo-data`.

**Tạm dừng mà không mất gì** (nhanh hơn down/up):
```powershell
docker compose stop     # tạm dừng
docker compose start    # chạy lại
```

---

# 🧪 Test luồng thanh toán

## Chế độ Mock
1. App → **+** thêm món → tab **Giỏ hàng** → nhập địa chỉ → **Đặt hàng**
2. App mở trang cổng thanh toán (số tiền + nút xanh) → bấm **"Xác nhận thanh toán"**
3. Vuốt back về app → màn hình tự hiện **"🎉 Đã thanh toán!"**

## Chế độ PayOS (tiền thật)
1. Giống trên, nhưng app mở **trang PayOS thật**
2. **Quét QR bằng app ngân hàng** → chuyển tiền (chọn món rẻ nhất để test)
3. PayOS redirect → trang `/api/payment/return` → tự mở lại app (deep link `pizzaapp://`)
4. PayOS gọi webhook → đơn tự chuyển **Paid** → app poll thấy → hiện "Đã thanh toán"

---

# 🩺 Sự cố thường gặp

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| App trắng / không hiện gì | Renderer **Impeller** lỗi trên emulator | Luôn dùng `flutter run --no-enable-impeller` |
| `Lost connection to device` | **Hết RAM** (Gradle daemon ăn ~900MB) | `taskkill /F /IM java.exe`, mở app bằng `adb monkey` thay vì `flutter run` |
| Emulator lag/đứng kinh khủng | Dùng `-gpu swiftshader_indirect` (CPU) hoặc hết RAM | Dùng `-gpu host`; đóng Edge/Zalo |
| Docker tự tắt | RAM căng khi chạy cùng emulator | Đã cap WSL2 = 2GB ở `C:\Users\tamtr\.wslconfig`. Đóng bớt app. |
| App không thấy sản phẩm | Backend chưa lên / sai baseUrl | `docker compose ps`; `curl http://localhost:8090/api/category` |
| Trang thanh toán `ERR_CONNECTION_REFUSED` | `PUBLIC_BASE_URL` sai với thiết bị | Emulator → `http://10.0.2.2:8090`; điện thoại thật → IP LAN |
| Trả tiền xong đơn không chuyển "Đã thanh toán" | Webhook PayOS chưa khai / ngrok tắt / URL đổi | Bật ngrok, cập nhật `.env`, khai lại webhook trong my.payos.vn |
| Đơn cũ mở link lỗi | Đơn lưu URL cũ trước khi đổi `.env` | Đặt **đơn mới** |
| `invalid command-line parameter` khi mở emulator | Dán dính chữ thừa vào lệnh | Copy đúng dòng lệnh, không kèm text |
| **App báo sai mật khẩu / "Đăng ký thất bại" dù backend OK** | **Phần mềm khác giành cổng** (xem mục dưới) | Kiểm tra ai chiếm cổng, đổi cổng hoặc tắt nó |

## 🔌 Xung đột cổng — bug từng gặp, RẤT khó phát hiện

**Triệu chứng:** `curl localhost` chạy ngon, nhưng app/điện thoại gọi vào thì lỗi lạ.

**Nguyên nhân thật:** 2 chương trình cùng bind 1 cổng nhưng **khác họ địa chỉ** → Windows KHÔNG báo lỗi:
```
::       :8080  ← Docker    (IPv6)   -> curl localhost trúng cái này  ✅
0.0.0.0  :8080  ← Apache    (IPv4)   -> app/emulator trúng cái này    ❌
```
Thủ phạm từng gặp: service **`PEMHTTPD-x64`** (Apache của EDB Postgres Enterprise Manager, cài kèm PostgreSQL, tự bật cùng Windows).

**Vì vậy dự án này dùng cổng `8090`, không dùng 8080.**

### Lệnh xem ai chiếm cổng
```powershell
# Xem ai LISTEN cổng (dễ đọc nhất)
Get-NetTCPConnection -LocalPort 8090 -State Listen |
  Select-Object LocalAddress, LocalPort, OwningProcess,
    @{N='Process';E={(Get-Process -Id $_.OwningProcess).ProcessName}}

# Cách cũ
netstat -ano | findstr :8090      # cột cuối = PID
tasklist /FI "PID eq 1234"        # PID đó là app gì

# Kill
taskkill /F /PID 1234
taskkill /F /IM httpd.exe
```

### Nếu là Windows Service (kill xong tự sống lại)
```powershell
Get-CimInstance Win32_Service | Where-Object { $_.ProcessId -eq 1234 } | Select Name, PathName
Stop-Service TEN_SERVICE
Set-Service TEN_SERVICE -StartupType Manual   # cấm tự bật cùng Windows
```

### Kiểm tra đúng cách (phải test bằng IPv4, KHÔNG chỉ localhost)
```powershell
curl http://localhost:8090/api/category         # IPv6 - có thể lừa bạn
curl http://192.168.1.132:8090/api/category     # IPv4 - đường app THẬT SỰ đi
```
Xem header `Server:` → phải là **Kestrel** (của mình). Nếu ra **Apache/nginx** ⇒ bị giành cổng.

---

# 🚀 CI/CD (GitHub Actions)

File: `.github/workflows/ci-cd.yml` — chạy khi push lên `main` / `tam-mircoservice`.

| Job | Khi nào chạy | Làm gì |
|---|---|---|
| **build-test** | Mọi push + PR | `dotnet build` + `dotnet test` (7 project) |
| **docker** | Push (không phải PR) | Build **7 image** song song (matrix) → push lên `ghcr.io` |
| **deploy** | Push vào `main` **và** đã khai secret `VPS_HOST` | SSH vào VPS → `docker compose pull && up -d` |

> **Chưa có VPS?** Không sao — job `deploy` **tự bỏ qua** (in notice), 2 job đầu vẫn chạy. Image vẫn được đẩy lên ghcr.io.

### Image sinh ra
```
ghcr.io/benz2222/pizzaapp/gateway:latest   (+ tag theo commit SHA)
ghcr.io/benz2222/pizzaapp/auth:latest
ghcr.io/benz2222/pizzaapp/{category,product,cart,order,payment}:latest
```

### Muốn bật deploy VPS — khai secrets
GitHub repo → **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Ví dụ |
|---|---|
| `VPS_HOST` | `123.45.67.89` |
| `VPS_USER` | `root` |
| `VPS_SSH_KEY` | Nội dung private key (`cat ~/.ssh/id_rsa`) |
| `JWT_SECRET_KEY` | chuỗi bí mật mới |
| `PUBLIC_BASE_URL` | `http://123.45.67.89:8090` |
| `PAYMENT_PROVIDER` | `PayOS` hoặc `Mock` |
| `PAYOS_CLIENT_ID` / `PAYOS_API_KEY` / `PAYOS_CHECKSUM_KEY` | key PayOS |

VPS chỉ cần cài sẵn **Docker**. Workflow tự copy `docker-compose.prod.yml`, tự tạo `.env`, tự pull image và chạy.

> 💡 Có VPS IP public thì **không cần ngrok nữa** — webhook PayOS gọi thẳng vào VPS.

### Chạy thủ công
GitHub → tab **Actions** → **CI/CD Microservices** → **Run workflow**

---

# 📁 Ghi chú cấu hình

**File `.env`** (`D:\PizzaApp\backend\.env`) — **không commit**:
```env
JWT_SECRET_KEY=...
PUBLIC_BASE_URL=...            # nơi app/điện thoại gọi tới gateway
PAYMENT_PROVIDER=Mock|PayOS    # đổi cổng thanh toán, KHÔNG cần sửa code
PAYOS_CLIENT_ID=...
PAYOS_API_KEY=...
PAYOS_CHECKSUM_KEY=...
PAYOS_RETURN_URL=...
PAYOS_CANCEL_URL=...
```

**baseUrl của app** (`pizza_flutter/lib/core/constants.dart`):
- Emulator: `http://10.0.2.2:8090/api`
- Web: `http://localhost:8090/api`
- Điện thoại thật: `http://<IP-LAN>:8090/api` (vd `192.168.1.132`)

**Cổng dịch vụ**: Gateway `8090` (công khai) · Mongo `27017` · RabbitMQ `5672`, UI quản trị `15672`
Các microservice (auth/category/product/cart/order/payment) **không expose ra ngoài** — chỉ gọi qua Gateway.

**Xem RAM**: Task Manager (`Ctrl+Shift+Esc`) → tab Processes → sort cột Memory. Docker: `docker stats`.
