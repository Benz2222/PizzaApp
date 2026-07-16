import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';
import '../models/category.dart';
import 'auth_service.dart';

class CategoryService {
  static Future<Map<String, String>> _authHeaders() async {
    final token = await AuthService.getToken();
    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  static Future<List<Category>> getAll() async {
    try {
      final res = await http.get(Uri.parse('${AppConstants.baseUrl}/category'));
      if (res.statusCode == 200) {
        final List data = jsonDecode(res.body);
        return data.map((e) => Category.fromJson(e)).toList();
      }
    } catch (_) {}
    return [];
  }

  /// Lấy danh sách TÊN danh mục (dùng để lọc ở trang chủ).
  static Future<List<String>> getNames() async {
    final cats = await getAll();
    return cats.map((c) => c.name).where((s) => s.isNotEmpty).toList();
  }

  // --- ADMIN ---

  static Future<String?> create(String name) async {
    final res = await http.post(
      Uri.parse('${AppConstants.baseUrl}/category'),
      headers: await _authHeaders(),
      body: jsonEncode({'name': name}),
    );
    return (res.statusCode == 200 || res.statusCode == 201)
        ? null
        : _error(res, 'Tạo danh mục thất bại');
  }

  static Future<String?> update(String id, String name) async {
    final res = await http.put(
      Uri.parse('${AppConstants.baseUrl}/category/$id'),
      headers: await _authHeaders(),
      body: jsonEncode({'name': name}),
    );
    return (res.statusCode == 200 || res.statusCode == 204)
        ? null
        : _error(res, 'Cập nhật thất bại');
  }

  static Future<String?> delete(String id) async {
    final res = await http.delete(
      Uri.parse('${AppConstants.baseUrl}/category/$id'),
      headers: await _authHeaders(),
    );
    return (res.statusCode == 200 || res.statusCode == 204)
        ? null
        : _error(res, 'Xóa thất bại');
  }

  static String _error(http.Response res, String fallback) {
    try {
      return jsonDecode(res.body)['message'] ?? fallback;
    } catch (_) {
      return fallback;
    }
  }
}
