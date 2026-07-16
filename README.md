# 🍕 PizzaApp — Hệ thống đặt pizza (Microservice)

App đặt pizza gồm **backend microservice (.NET 8)** + **app Flutter**, thanh toán **QR PayOS**.

| Thư mục | Nội dung |
|---|---|
| `backend/` | 6 microservice + API Gateway (hệ thống chính) |
| `pizza_flutter/` | App Android (Flutter) |
| `PizzaApp/` | Monolith cũ — **giữ lại để đối chiếu**, không chạy nữa |
| `docs/` | Tài liệu thiết kế & kế hoạch migration |
| `RUNBOOK.md` | Hướng dẫn vận hành chi tiết + xử lý sự cố |

---

## 🚀 Chạy trên máy mới — 6 bước

### Bước 1. Cài phần mềm

| Phần mềm | Link | Ghi chú |
|---|---|---|
| **Docker Desktop** | docker.com/products/docker-desktop | Chọn bản **AMD64** (CPU Intel/AMD) |
| **Flutter SDK** | docs.flutter.dev/get-started/install | |
| **Android Studio** | developer.android.com/studio | Để có emulator + SDK |
| **Git** | git-scm.com | |

> ❗ **KHÔNG cần cài .NET SDK** — Docker tự build backend bên trong container.

Windows Home: cài Docker cần bật WSL2 trước → mở PowerShell **Admin** chạy `wsl --install` rồi khởi động lại máy.

### Bước 2. Tải code
```powershell
git clone https://github.com/Benz2222/PizzaApp.git
cd PizzaApp
```

### Bước 3. Tạo file cấu hình
```powershell
cd backend
copy .env.example .env
```
Mở `.env` sửa:
```env
MONGO_CONNECTION=mongodb+srv://...    # ⬅ HỎI CHỦ REPO lấy chuỗi Atlas
PUBLIC_BASE_URL=http://10.0.2.2:8090  # emulator. Máy thật: http://<IP-LAN>:8090
PAYMENT_PROVIDER=Mock                 # Mock = giả lập (không tiền thật)
```
> **`MONGO_CONNECTION` là bắt buộc** — chuỗi Atlas không nằm trong repo (bí mật). Xin chủ repo.
> Muốn dùng **PayOS tiền thật** → xem [RUNBOOK.md](RUNBOOK.md) mục "Chế độ PayOS" (cần ngrok).

### Bước 4. Chạy backend
```powershell
# Mở Docker Desktop, đợi hiện "Engine running"
docker compose up -d --build
```
> Lần đầu mất **5–10 phút** (build 7 image). Các lần sau chỉ vài giây.

Kiểm tra:
```powershell
docker compose ps                            # phải thấy 9 container "Up"
curl http://localhost:8090/api/category      # phải trả JSON
```

### Bước 5. Dữ liệu

**Dùng Atlas (mặc định):** ✅ **Bỏ qua bước này** — dữ liệu đã có sẵn trên cloud, mọi máy dùng chung.

**Dùng Docker Mongo** (`MONGO_CONNECTION=mongodb://mongo:27017`): database sẽ **trống** → phải nạp:
```powershell
.\seed-data.ps1
```
> Script tự đọc `MONGO_CONNECTION` trong `.env` → nạp đúng đích (Atlas hoặc local).
> Nạp sẵn **22 tài khoản, 4 danh mục, 4 sản phẩm**. Cũng dùng được để **reset dữ liệu về ban đầu**.

### Bước 6. Chạy app
```powershell
# Mở emulator (giữ cửa sổ này)
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd Pixel_4 -gpu host

# Cửa sổ khác
cd ..\pizza_flutter
flutter pub get
flutter run --no-enable-impeller
```
> **`--no-enable-impeller` là BẮT BUỘC** — thiếu nó app hiện màn hình trắng trên emulator.

---

## 🔑 Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| **Admin** | `admin@pizza.com` | `admin123` |

Hoặc bấm **"Đăng ký ngay"** trong app để tạo tài khoản khách mới.

## ✨ Tính năng chính

**Khách hàng:** xem món theo danh mục · tìm kiếm · giỏ hàng · đặt hàng · **thanh toán QR (PayOS)** · theo dõi đơn

**Admin** (menu trong tab *Tài khoản*):
- **Bảng điều khiển** — doanh thu hôm nay/tổng, số đơn, đơn theo trạng thái, top món bán chạy, tổng quan hệ thống
- Quản lý đơn hàng / sản phẩm / danh mục

**Shipper:** nhận đơn · cập nhật trạng thái giao

> 💡 **Dashboard là ví dụ rõ nhất về microservice**: nó gọi **song song 4 endpoint** (`orders`/`auth`/`products`/`category` `/admin/stats`) — mỗi service tự tính số liệu của mình. **Một service chết cũng không sập cả màn hình**: phần đó hiện `—`, phần còn lại vẫn chạy.

---

## 🏗️ Kiến trúc

```
        Flutter App
             │
             ▼
     API Gateway (YARP) :8090        ← cổng công khai duy nhất
             │
   ┌────┬────┼────┬─────┬──────┐
   ▼    ▼    ▼    ▼     ▼      ▼
 Auth Category Product Cart Order Payment    ← 6 microservice
   │    │    │    │     │      │
   └────┴────┴────┴─────┴──────┘
      MongoDB (mỗi service 1 DB riêng)
             +
        RabbitMQ (event)
```

**Nguyên tắc:**
- **Database per service** — mỗi service 1 DB riêng, không dùng chung
- **REST đồng bộ** khi cần trả lời ngay (Order → Product lấy giá)
- **RabbitMQ event** cho việc chạy nền: `OrderCreated` → xoá giỏ · `PaymentSucceeded` → đơn thành Paid
- **Thanh toán trừu tượng** qua `IPaymentGateway` → đổi Mock ↔ PayOS bằng 1 biến env, không sửa code

---

## 🛑 Tắt hệ thống
```powershell
docker compose down          # giữ dữ liệu
docker compose down -v       # ⚠️ XOÁ SẠCH database
```

---

## 📖 Xem thêm

- **[RUNBOOK.md](RUNBOOK.md)** — vận hành chi tiết, PayOS/ngrok, CI/CD, **xử lý sự cố**
- `docs/superpowers/specs/` — tài liệu thiết kế
- `docs/superpowers/plans/` — kế hoạch migration monolith → microservice

## ⚡ Lỗi hay gặp

| Lỗi | Cách sửa |
|---|---|
| App trắng màn hình | Dùng `flutter run --no-enable-impeller` |
| App không có sản phẩm / không đăng nhập được | Sai/thiếu `MONGO_CONNECTION` trong `.env`, hoặc dùng Docker Mongo mà chưa chạy `.\seed-data.ps1` |
| `Lost connection to device` | Hết RAM → `taskkill /F /IM java.exe` |
| Đăng nhập sai dù mật khẩu đúng | Phần mềm khác giành cổng 8090 → xem RUNBOOK mục "Xung đột cổng" |

Chi tiết đầy đủ: [RUNBOOK.md](RUNBOOK.md)
