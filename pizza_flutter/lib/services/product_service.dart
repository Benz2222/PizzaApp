import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';
import '../models/product.dart';

class ProductService {
  static Future<List<Product>> getAll() async {
    final res = await http.get(
      Uri.parse('${AppConstants.baseUrl}/products'),
    );
    if (res.statusCode == 200) {
      final List data = jsonDecode(res.body);
      return data.map((e) => Product.fromJson(e)).toList();
    }
    return [];
  }
}