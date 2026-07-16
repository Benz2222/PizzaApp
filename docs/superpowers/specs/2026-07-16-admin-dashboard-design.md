# Admin Dashboard — Thiết kế

**Ngày:** 2026-07-16
**Trạng thái:** Đã duyệt, chờ lập kế hoạch triển khai
**Bối cảnh:** Thêm màn hình dashboard cho Admin trong app Flutter, lấy số liệu từ hệ thống microservice.

## 1. Mục tiêu

Admin mở app thấy ngay bức tranh tổng quan: doanh thu, số đơn, đơn theo trạng thái, món bán chạy, và tổng quan hệ thống (số user/sản phẩm/danh mục).

## 2. Quyết định kiến trúc

**Mỗi service tự expose endpoint `/admin/stats` của riêng mình; app gọi song song 4 endpoint qua Gateway.**

```
App ──┬─> GET /api/orders/admin/stats     (Order service)
      ├─> GET /api/auth/admin/stats       (Auth service)
      ├─> GET /api/products/admin/stats   (Product service)
      └─> GET /api/category/admin/stats   (Category service)
         (Future.wait -> 4 request song song, ~1 vòng mạng)
```

### Vì sao chọn hướng này
- **Service sở hữu dữ liệu của nó** — không service nào gọi chéo để lấy số liệu của service khác
- **Tính toán tại nơi có dữ liệu** — dùng MongoDB aggregation, không tải dữ liệu thô về client
- **Không phình kiến trúc** — không thêm service thứ 7, không nhét logic nghiệp vụ vào Gateway (YARP giữ đúng vai trò reverse proxy)

### Phương án đã loại
| Phương án | Lý do loại |
|---|---|
| **BFF** (Gateway/service riêng gom số) | Phá vai trò reverse proxy của Gateway, hoặc đẻ thêm service thứ 7 — thừa cho quy mô đồ án |
| **App tự tính từ dữ liệu thô** | Tải toàn bộ đơn về điện thoại → lag khi nhiều đơn; Auth cũng không có API đếm user nên vẫn phải sửa backend |

## 3. Endpoint & payload

Tất cả đều `[Authorize(Roles = "Admin")]`.

### `GET /api/orders/admin/stats`
```json
{
  "revenueToday": 250000,
  "revenueTotal": 5400000,
  "ordersToday": 3,
  "ordersTotal": 42,
  "byStatus": {
    "AwaitingPayment": 2, "Paid": 5, "Preparing": 1,
    "Ready": 0, "Delivering": 2, "Done": 30, "Cancelled": 2
  },
  "topProducts": [
    { "productName": "Margherita", "quantity": 45, "revenue": 4005000 }
  ]
}
```

### `GET /api/auth/admin/stats`
```json
{ "totalUsers": 22, "byRole": { "Customer": 19, "Admin": 2, "Shipper": 1 } }
```

### `GET /api/products/admin/stats`
```json
{ "totalProducts": 4, "available": 4, "unavailable": 0 }
```

### `GET /api/category/admin/stats`
```json
{ "totalCategories": 4 }
```

## 4. Quy tắc nghiệp vụ

- **`revenueTotal`** = tổng `TotalPrice` của đơn có `PaymentStatus == "Paid"` **và** `Status != "Cancelled"`.
- **`revenueToday`** = như trên, thêm điều kiện `CreatedAt` trong hôm nay.
- **`ordersTotal`** = đếm **mọi** đơn (kể cả chưa trả tiền, kể cả đã huỷ) — đây là số đơn, không phải doanh thu.
- **`ordersToday`** = đếm **mọi** đơn tạo trong hôm nay (cùng quy tắc với `ordersTotal`, chỉ thêm lọc ngày).
- **"Hôm nay"** = từ `00:00:00` đến `23:59:59` **theo UTC**, khớp với `CreatedAt` đang lưu trong DB.
- **`topProducts`** = 5 món bán chạy nhất theo **số lượng**, chỉ tính từ đơn **đã thanh toán**.
- **`byStatus`** = đếm đơn theo `Status`, gồm đủ 7 trạng thái (giá trị 0 nếu không có đơn nào).

## 5. Phía app (Flutter)

**Điểm vào:** `account_screen.dart` → thêm mục **"Bảng điều khiển"** ở **trên cùng** danh sách menu admin (trên "Quản lý đơn hàng").

**File mới:**
- `lib/services/dashboard_service.dart` — gọi 4 endpoint bằng `Future.wait`
- `lib/models/dashboard.dart` — model cho 4 payload
- `lib/screens/admin_dashboard_screen.dart` — màn hình

**Giao diện** (dùng lại style cam-trắng sẵn có, **không chế màu mới**):
- Hàng thẻ số liệu: Doanh thu hôm nay · Doanh thu tổng · Đơn hôm nay · Tổng đơn
- Đơn theo trạng thái: danh sách kèm chấm màu — **dùng lại màu từ `core/order_status.dart`**
- Top 5 món bán chạy: tên · số lượng · doanh thu
- Tổng quan hệ thống: khách hàng / shipper · sản phẩm · danh mục
- Pull-to-refresh

## 6. Xử lý lỗi — resilience

**Nguyên tắc: 1 service chết KHÔNG được kéo sập cả dashboard.**

`Future.wait` với từng call bọc `try/catch` riêng → service nào lỗi thì phần đó hiện `—` kèm chú thích "Không tải được", các phần còn lại vẫn hiển thị bình thường.

Đây là điểm khác biệt cốt lõi so với monolith (chết là chết cả) và là minh chứng rõ cho tính resilience của microservice.

## 7. Kiểm thử

- **Unit test** cho logic thuần ở mỗi service:
  - Tính doanh thu: loại đơn chưa trả tiền và đơn đã huỷ
  - Gom `byStatus`: đủ 7 trạng thái, trạng thái không có đơn → 0
  - `topProducts`: sắp xếp giảm dần theo số lượng, cắt còn 5
- **Verify thật**: gọi 4 endpoint qua Gateway bằng `curl` với token Admin, đối chiếu số liệu với dữ liệu thật trên Atlas.
- MongoDB aggregation không unit-test được → dựa vào verify thật.

## 8. Ngoài phạm vi (YAGNI)

- Không biểu đồ (chart)
- Không lọc theo khoảng ngày
- Không export Excel/PDF
- Không realtime (chỉ pull-to-refresh)
- Không cache
