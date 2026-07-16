# PizzaApp — Chuyển đổi sang kiến trúc Microservice

**Ngày:** 2026-07-14
**Trạng thái:** Đã duyệt thiết kế, chờ lập kế hoạch triển khai
**Bối cảnh:** Đồ án học tập / portfolio. Ưu tiên đúng khái niệm microservice, demo được bằng Docker Compose trên 1 máy, đơn giản nhưng đầy đủ.

## 1. Mục tiêu

Chuyển hệ thống PizzaApp từ **monolith** (ASP.NET Core Web API, Clean Architecture, MongoDB, PayOS, JWT) sang **kiến trúc microservice** phục vụ đồ án. Client chính là **Flutter mobile app** (`pizza_flutter`) — dùng để chấm; Flutter web chỉ là phụ (cần build pass, không ưu tiên).

### Quyết định cốt lõi
- **6 microservice + API Gateway**, monorepo.
- **Database per service** (mỗi service 1 MongoDB database riêng).
- Giao tiếp: **REST đồng bộ** khi cần phản hồi ngay + **RabbitMQ** cho sự kiện bất đồng bộ.
- Gateway: **YARP**. JWT dùng chung, **tách secrets** khỏi `appsettings.json`.
- **CI/CD** GitHub Actions: build/test (matrix) → build & push Docker image lên `ghcr.io` → auto-deploy qua **SSH + Docker Compose** vào VPS.

## 2. Kiến trúc tổng thể

```
                    ┌─────────────┐
   Flutter App ───► │ API Gateway │  (YARP, cổng công khai duy nhất, :8080)
                    │             │  - Routing tới service
                    └──────┬──────┘  - Validate JWT trước khi chuyển tiếp
                           │
     ┌─────────┬───────────┼───────────┬──────────┬──────────┐
     ▼         ▼           ▼           ▼          ▼          ▼
 ┌───────┐ ┌────────┐ ┌─────────┐ ┌───────┐ ┌───────┐ ┌─────────┐
 │ Auth  │ │Product │ │Category │ │ Cart  │ │ Order │ │ Payment │
 │Service│ │Service │ │ Service │ │Service│ │Service│ │ Service │
 └───┬───┘ └───┬────┘ └────┬────┘ └───┬───┘ └───┬───┘ └────┬────┘
     │DB       │DB         │DB        │DB       │DB        │DB
   Auth      Product    Category    Cart     Order     Payment
     │         │           │          │         │          │
     └─────────┴───────────┴──────────┴─────────┴──────────┘
                    RabbitMQ (event bus bất đồng bộ)
```

**Thành phần:**
- **6 microservice** độc lập — mỗi cái là 1 ASP.NET Core Web API riêng, MongoDB database riêng.
- **API Gateway (YARP)** — điểm vào duy nhất cho mobile app; routing + validate JWT.
- **RabbitMQ** — bus sự kiện cho giao tiếp bất đồng bộ.
- **BuildingBlocks** (project chung) — code dùng lại: event contracts, JWT helper, base MongoDB, RabbitMQ wrapper. Không chứa business logic.
- Chạy toàn bộ bằng **Docker Compose**: 6 service + gateway + mongo + rabbitmq.

## 3. Các service & dữ liệu

| Service | Cổng | Database | Collections sở hữu | Nhiệm vụ |
|---|---|---|---|---|
| **Auth** | 5001 | `PizzaApp_Auth` | Users | Đăng ký, đăng nhập, phát hành JWT, `/me` |
| **Product** | 5002 | `PizzaApp_Product` | Products | CRUD sản phẩm, search/paging, upload ảnh |
| **Category** | 5003 | `PizzaApp_Category` | Categories | CRUD danh mục |
| **Cart** | 5004 | `PizzaApp_Cart` | Carts | Giỏ hàng theo user |
| **Order** | 5005 | `PizzaApp_Order` | Orders | Tạo/hủy đơn, trạng thái, shipper nhận đơn |
| **Payment** | 5006 | `PizzaApp_Payment` | Payments | Tạo link PayOS, nhận webhook |

### Nguyên tắc dữ liệu
- Mỗi service chỉ đọc/ghi DB của chính nó. Không service nào truy cập collection của service khác.
- **Denormalize thay vì join**: `OrderItem` lưu sẵn `ProductName`, `ProductImageUrl`, `UnitPrice`, `Size` (đã có sẵn trong entity hiện tại) → Order không cần gọi Product khi hiển thị lịch sử đơn.
- `UserId`, `ProductId`, `CategoryId` là tham chiếu "mềm" qua ID, không có khóa ngoại xuyên service.

### JWT dùng chung
- Auth Service ký JWT bằng secret key.
- Gateway và mọi service validate token bằng **cùng** secret (đưa vào biến môi trường / GitHub Secrets — **không hardcode** như hiện tại).
- Nhờ vậy mọi service tự đọc `userId`/`role` từ token, không cần gọi Auth cho mỗi request.

## 4. Luồng nghiệp vụ chính

### A. Đặt hàng & thanh toán (luồng quan trọng nhất — được chấm)

```
1. Mobile → Gateway → Order Service: POST /orders (kèm JWT)
2. Order Service:
     - (REST đồng bộ) gọi Cart Service lấy giỏ hàng của user
     - (REST đồng bộ) gọi Product Service verify giá + tồn tại sản phẩm
     - Tạo Order (status=AwaitingPayment), lưu DB Order
     - (Event) publish "OrderCreated" lên RabbitMQ
3. Cart Service ← nghe "OrderCreated" → xóa giỏ hàng (async)
4. Mobile → Gateway → Payment Service: POST /payments/create (orderId)
     - Payment tạo link PayOS, lưu Payment (DB Payment)
5. PayOS → Payment Service: POST /payments/webhook (xác thực chữ ký)
     - Payment cập nhật Payment=Paid
     - (Event) publish "PaymentSucceeded" lên RabbitMQ
6. Order Service ← nghe "PaymentSucceeded" → cập nhật Order status=Paid (async)
```

**Vì sao dùng cả REST và event:**
- REST đồng bộ khi cần câu trả lời ngay (lấy cart, verify sản phẩm).
- Event bất đồng bộ khi hành động phụ, không cần chờ (xóa cart, đổi trạng thái order sau thanh toán) → giảm phụ thuộc trực tiếp; nếu Order tạm chết thì xử lý lại khi RabbitMQ retry.

### B. Các luồng đơn giản
Auth, xem sản phẩm, danh mục, giỏ hàng: mobile → Gateway → thẳng service tương ứng, không cần event.

### Xử lý lỗi
- Gọi REST liên service thất bại → trả lỗi rõ ràng cho client (không tạo đơn treo). Có timeout + retry ngắn.
- Event thất bại → RabbitMQ giữ message, consumer retry; consumer phải **idempotent** (xử lý trùng không sai).

## 5. Cấu trúc repo (monorepo)

```
backend/
  src/
    ApiGateway/                 # YARP
    Services/
      Auth/     (Auth.API, Auth.Core, Auth.Infrastructure)
      Product/  (Product.API, Product.Core, Product.Infrastructure)
      Category/ (...)
      Cart/     (...)
      Order/    (...)
      Payment/  (...)
    BuildingBlocks/             # Events, JwtHelper, MongoBase, RabbitMQ wrapper
  tests/                        # test cho mỗi service
  docker-compose.yml            # chạy toàn bộ local (build từ source)
  docker-compose.prod.yml       # dùng image từ ghcr.io cho deploy
PizzaApp/                       # monolith cũ — giữ để tham chiếu, xóa ở bước cuối
pizza_flutter/                  # mobile app — đổi baseUrl → Gateway
```

Mỗi service giữ cấu trúc Clean Architecture nhẹ (API / Core / Infrastructure) đồng nhất với monolith hiện tại để dễ chuyển code.

## 6. CI/CD & Deployment

### GitHub Actions — `.github/workflows/microservices-ci-cd.yml`
- **Trigger:** push / pull_request vào `main`.
- **Job `build-test`** (matrix theo từng service, chạy song song): `dotnet restore/build/test`. PR chỉ dừng ở job này.
- **Job `docker-build-push`** (chỉ khi push `main`): build Docker image mỗi service + gateway, tag theo commit SHA + `latest`, push lên `ghcr.io`.
- **Job `deploy`** (sau khi push image thành công): SSH vào VPS (secrets `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`) → `docker compose -f docker-compose.prod.yml pull && up -d`.

### Secrets (GitHub Secrets — không commit)
JWT key, MongoDB connection string, PayOS ClientId/ApiKey/ChecksumKey, VPS SSH host/user/key. Đây cũng là dịp tách secrets ra khỏi `appsettings.json`.

### Deployment local (demo/chấm)
Một lệnh `docker compose up` khởi động: 6 service + gateway + MongoDB + RabbitMQ. Mobile app trỏ vào IP của Gateway.

## 7. Chiến lược migrate (tăng dần, luôn có bản chạy được)

1. Dựng khung: `BuildingBlocks`, docker-compose (Mongo + RabbitMQ + Gateway rỗng).
2. Tách **Auth** trước (mọi service phụ thuộc JWT) → verify đăng nhập qua Gateway.
3. Tách **Category**, rồi **Product** (đơn giản, ít phụ thuộc).
4. Tách **Cart**.
5. Tách **Order** + wiring REST tới Cart/Product + event `OrderCreated`.
6. Tách **Payment** + PayOS webhook + event `PaymentSucceeded` ↔ Order.
7. Đổi `baseUrl` trong `pizza_flutter` sang Gateway, chạy end-to-end trên mobile.
8. Xóa monolith `PizzaApp/` cũ + workflow CI cũ.

Mỗi bước copy code từ monolith hiện có (logic đã chạy tốt), chỉ tách ranh giới + thay cách truy cập DB/service.

## 8. Kiểm thử

- **Unit/integration mỗi service**: tách tinh thần bộ `PizzaApp.IntegrationTests` sẵn có theo từng service; CI chạy tự động.
- **Smoke test luồng chính**: script kiểm tra qua Gateway (đăng nhập → xem sản phẩm → thêm giỏ → đặt đơn → thanh toán) — đúng luồng chấm đồ án trên mobile.
- Ưu tiên mobile; Flutter web chỉ cần build pass.

## 9. Ngoài phạm vi (YAGNI)

- Không dùng Kubernetes / service mesh / distributed tracing nâng cao.
- Không tách thêm service ngoài 6 service trên.
- Không làm authz phức tạp ngoài JWT role sẵn có.
- Không tối ưu hiệu năng/scale thực tế (đây là đồ án demo 1 máy + 1 VPS).
