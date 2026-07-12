import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../core/constants.dart';

class AuthService {
  // Lưu token vào bộ nhớ
  static Future<void> saveToken(String token) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('token', token);
  }

  // Lấy token ra dùng
  static Future<String?> getToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString('token');
  }

  // Xóa token khi logout
  static Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('token');
  }

  // Đăng ký — trả null nếu thành công, ngược lại trả chuỗi lỗi để hiển thị
  static Future<String?> register(String fullName, String email,
      String password, String phone) async {
    try {
      final res = await http.post(
        Uri.parse('${AppConstants.baseUrl}/auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'fullName': fullName,
          'email': email,
          'password': password,
          'phoneNumber': phone,
        }),
      );
      if (res.statusCode == 200) {
        final token = jsonDecode(res.body)['data']?['token'];
        if (token != null) {
          await saveToken(token);
          return null;
        }
      }
      return _parseError(res.body, 'Đăng ký thất bại');
    } catch (_) {
      return 'Không kết nối được máy chủ';
    }
  }

  // Đăng nhập — trả null nếu thành công, ngược lại trả chuỗi lỗi
  static Future<String?> login(String email, String password) async {
    try {
      final res = await http.post(
        Uri.parse('${AppConstants.baseUrl}/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': email, 'password': password}),
      );
      if (res.statusCode == 200) {
        final token = jsonDecode(res.body)['data']?['token'];
        if (token != null) {
          await saveToken(token);
          return null;
        }
      }
      return _parseError(res.body, 'Email hoặc mật khẩu không đúng');
    } catch (_) {
      return 'Không kết nối được máy chủ';
    }
  }

  // Quên mật khẩu — trả token (dev BE trả token trong response) hoặc lỗi
  static Future<({String? token, String? error})> forgotPassword(
      String email) async {
    try {
      final res = await http.post(
        Uri.parse('${AppConstants.baseUrl}/auth/forgot-password'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'email': email}),
      );
      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        return (token: data['token']?.toString(), error: null);
      }
      return (token: null, error: _parseError(res.body, 'Không gửi được yêu cầu'));
    } catch (_) {
      return (token: null, error: 'Không kết nối được máy chủ');
    }
  }

  // Đặt lại mật khẩu — trả null nếu thành công, ngược lại trả chuỗi lỗi
  static Future<String?> resetPassword(
      String email, String token, String newPassword) async {
    try {
      final res = await http.post(
        Uri.parse('${AppConstants.baseUrl}/auth/reset-password'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'email': email,
          'token': token,
          'newPassword': newPassword,
        }),
      );
      if (res.statusCode == 200) return null;
      return _parseError(res.body, 'Đổi mật khẩu thất bại');
    } catch (_) {
      return 'Không kết nối được máy chủ';
    }
  }

  // Lấy thông tin user đang đăng nhập (GET /auth/me)
  static Future<Map<String, dynamic>?> getMe() async {
    try {
      final token = await getToken();
      final res = await http.get(
        Uri.parse('${AppConstants.baseUrl}/auth/me'),
        headers: {'Authorization': 'Bearer $token'},
      );
      if (res.statusCode == 200) {
        return jsonDecode(res.body) as Map<String, dynamic>;
      }
    } catch (_) {}
    return null;
  }

  // Lấy thông báo lỗi thật từ response của BE
  static String _parseError(String body, String fallback) {
    try {
      final data = jsonDecode(body);
      if (data['message'] != null) return data['message'];
      // Lỗi validation dạng { errors: { Field: ["thông báo"] } }
      if (data['errors'] is Map) {
        final first = (data['errors'] as Map).values.first;
        if (first is List && first.isNotEmpty) return first.first.toString();
      }
    } catch (_) {}
    return fallback;
  }
}