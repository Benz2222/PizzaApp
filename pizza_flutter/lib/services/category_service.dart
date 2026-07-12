import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';

class CategoryService {
  /// Lấy danh sách TÊN danh mục từ BE (dùng để lọc sản phẩm ở trang chủ).
  static Future<List<String>> getNames() async {
    try {
      final res = await http.get(
        Uri.parse('${AppConstants.baseUrl}/category'),
      );
      if (res.statusCode == 200) {
        final List data = jsonDecode(res.body);
        return data
            .map((e) => (e['name'] ?? '').toString())
            .where((s) => s.isNotEmpty)
            .toList();
      }
    } catch (_) {}
    return [];
  }
}
