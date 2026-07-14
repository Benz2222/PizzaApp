import 'package:flutter/foundation.dart' show kIsWeb;

class AppConstants {
  // Tự chọn địa chỉ API theo nền tảng:
  // - Web (Chrome/Edge/Windows): localhost
  // - Android Emulator: 10.0.2.2 (= localhost của máy tính)
  // Thiết bị thật: đổi thành IP LAN của máy, VD: http://192.168.1.5:5211/api
  static String get baseUrl =>
      kIsWeb ? 'http://localhost:8080/api' : 'http://10.0.2.2:8080/api';

  // Host phục vụ ảnh (bỏ phần /api)
  static String get imageHost => baseUrl.replaceAll('/api', '');

  // Dựng URL ảnh đầy đủ. Trả null nếu không có ảnh thật (vd tên file seed cũ) -> hiển thị emoji.
  static String? fullImageUrl(String imageUrl) {
    if (imageUrl.isEmpty) return null;
    if (imageUrl.startsWith('http')) return imageUrl;
    if (imageUrl.startsWith('/')) return imageHost + imageUrl;
    return null;
  }
}
