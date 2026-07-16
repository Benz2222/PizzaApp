import 'package:flutter/foundation.dart' show kIsWeb;

class AppConstants {
  // Backend chạy trên server Azure (công khai 24/7) — dùng cho MỌI nền tảng
  // (emulator, web, và cả điện thoại thật).
  static const String _serverUrl = 'http://20.205.122.230:8090/api';

  // Muốn quay lại chạy backend LOCAL (Docker trên máy) thì đổi _useServer = false.
  static const bool _useServer = true;

  static String get baseUrl {
    if (_useServer) return _serverUrl;
    // Backend local: emulator dùng 10.0.2.2, web dùng localhost.
    return kIsWeb ? 'http://localhost:8090/api' : 'http://10.0.2.2:8090/api';
  }

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
