import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:image_picker/image_picker.dart';
import '../core/constants.dart';
import '../models/product.dart';
import 'auth_service.dart';

class ProductService {
  static Future<Map<String, String>> _authHeaders() async {
    final token = await AuthService.getToken();
    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  static Future<List<Product>> getAll({String? search, String? categoryId}) async {
    final params = <String, String>{};
    if (search != null && search.isNotEmpty) params['search'] = search;
    if (categoryId != null && categoryId.isNotEmpty) params['categoryId'] = categoryId;
    final uri = Uri.parse('${AppConstants.baseUrl}/products')
        .replace(queryParameters: params.isEmpty ? null : params);
    final res = await http.get(uri);
    if (res.statusCode == 200) {
      final List data = jsonDecode(res.body);
      return data.map((e) => Product.fromJson(e)).toList();
    }
    return [];
  }

  // --- ADMIN ---

  static Future<String?> create({
    required String name,
    required String description,
    required double price,
    required String imageUrl,
    required String categoryId,
  }) async {
    final res = await http.post(
      Uri.parse('${AppConstants.baseUrl}/products'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'name': name,
        'description': description,
        'price': price,
        'imageUrl': imageUrl,
        'categoryId': categoryId,
      }),
    );
    return (res.statusCode == 200 || res.statusCode == 201)
        ? null
        : _error(res, 'Tạo sản phẩm thất bại');
  }

  static Future<String?> update({
    required String id,
    required String name,
    required String description,
    required double price,
    required String imageUrl,
    required String categoryId,
    required bool isAvailable,
  }) async {
    final res = await http.put(
      Uri.parse('${AppConstants.baseUrl}/products/$id'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'name': name,
        'description': description,
        'price': price,
        'imageUrl': imageUrl,
        'categoryId': categoryId,
        'isAvailable': isAvailable,
      }),
    );
    return (res.statusCode == 200 || res.statusCode == 204)
        ? null
        : _error(res, 'Cập nhật thất bại');
  }

  static Future<String?> delete(String id) async {
    final res = await http.delete(
      Uri.parse('${AppConstants.baseUrl}/products/$id'),
      headers: await _authHeaders(),
    );
    return (res.statusCode == 200 || res.statusCode == 204)
        ? null
        : _error(res, 'Xóa thất bại');
  }

  /// Upload ảnh -> trả về đường dẫn ảnh (imageUrl) hoặc null nếu lỗi.
  static Future<String?> uploadImage(XFile file) async {
    final token = await AuthService.getToken();
    final bytes = await file.readAsBytes();
    final req = http.MultipartRequest(
        'POST', Uri.parse('${AppConstants.baseUrl}/products/upload'));
    req.headers['Authorization'] = 'Bearer $token';
    req.files.add(http.MultipartFile.fromBytes('file', bytes,
        filename: file.name.isEmpty ? 'upload.jpg' : file.name));
    final streamed = await req.send();
    final res = await http.Response.fromStream(streamed);
    if (res.statusCode == 200) {
      return jsonDecode(res.body)['imageUrl']?.toString();
    }
    return null;
  }

  static String _error(http.Response res, String fallback) {
    try {
      return jsonDecode(res.body)['message'] ?? fallback;
    } catch (_) {
      return fallback;
    }
  }
}
