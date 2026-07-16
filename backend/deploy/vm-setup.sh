#!/usr/bin/env bash
# ============================================================
#  PizzaApp — script dựng backend trên VM Ubuntu (Azure/VPS)
#  Chạy MỘT LẦN trên VM sau khi SSH vào.
#  Dùng:  bash vm-setup.sh
# ============================================================
set -e

echo "==> [1/4] Cài Docker (nếu chưa có)"
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker "$USER"
  echo "   Đã cài Docker. (Có thể cần đăng xuất/đăng nhập lại để dùng docker không cần sudo)"
else
  echo "   Docker đã có sẵn."
fi

echo "==> [2/4] Tạo thư mục ~/pizzaapp"
mkdir -p ~/pizzaapp
cd ~/pizzaapp

echo "==> [3/4] Kiểm tra file cấu hình"
if [ ! -f docker-compose.prod.yml ]; then
  echo "   !! THIẾU docker-compose.prod.yml — hãy copy file này lên (xem hướng dẫn)."
  exit 1
fi
if [ ! -f .env ]; then
  echo "   !! THIẾU .env — hãy tạo từ .env.prod.example rồi điền bí mật."
  exit 1
fi

echo "==> [4/4] Kéo image & chạy"
# Nếu image ghcr.io để PRIVATE, cần login trước (bỏ comment 1 dòng dưới, thay TOKEN):
# echo "GHCR_TOKEN" | docker login ghcr.io -u YOUR_GITHUB_USER --password-stdin
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
echo ""
echo "==> XONG. Kiểm tra:"
docker compose -f docker-compose.prod.yml ps
echo ""
echo "Thử:  curl http://localhost:8090/api/category"
