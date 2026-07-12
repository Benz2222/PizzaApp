import 'package:flutter/foundation.dart' show kIsWeb;

class AppConstants {
  // Tự chọn địa chỉ API theo nền tảng:
  // - Web (Chrome/Edge/Windows): localhost
  // - Android Emulator: 10.0.2.2 (= localhost của máy tính)
  // Thiết bị thật: đổi thành IP LAN của máy, VD: http://192.168.1.5:5211/api
  static String get baseUrl =>
      kIsWeb ? 'http://localhost:5211/api' : 'http://10.0.2.2:5211/api';
}
