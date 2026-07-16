import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';
import '../models/dashboard.dart';
import 'auth_service.dart';

class DashboardService {
  static Future<Map<String, String>> _headers() async {
    final token = await AuthService.getToken();
    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  /// Gọi 1 endpoint. LỖI TRẢ NULL (không ném) -> 1 service chết không kéo sập dashboard.
  static Future<T?> _get<T>(
      String path, T Function(Map<String, dynamic>) parse) async {
    try {
      final res = await http
          .get(Uri.parse('${AppConstants.baseUrl}$path'), headers: await _headers())
          .timeout(const Duration(seconds: 10));
      if (res.statusCode != 200) return null;
      return parse(jsonDecode(res.body));
    } catch (_) {
      return null;
    }
  }

  /// Gọi 4 endpoint SONG SONG (~1 vòng mạng).
  static Future<DashboardData> load() async {
    final results = await Future.wait([
      _get('/orders/admin/stats', (j) => OrderStats.fromJson(j)),
      _get('/auth/admin/stats', (j) => AuthStats.fromJson(j)),
      _get('/products/admin/stats', (j) => ProductStats.fromJson(j)),
      _get('/category/admin/stats', (j) => CategoryStats.fromJson(j)),
    ]);

    return DashboardData(
      orders: results[0] as OrderStats?,
      auth: results[1] as AuthStats?,
      products: results[2] as ProductStats?,
      categories: results[3] as CategoryStats?,
    );
  }
}
