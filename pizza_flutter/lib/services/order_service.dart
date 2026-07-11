import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';
import '../models/order.dart';
import '../providers/cart_provider.dart';
import 'auth_service.dart';

class CheckoutResult {
  final String orderId;
  final String checkoutUrl;
  CheckoutResult({required this.orderId, required this.checkoutUrl});
}

class OrderService {
  static Future<Map<String, String>> _headers() async {
    final token = await AuthService.getToken();
    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  /// Đồng bộ giỏ hàng local lên server rồi gọi checkout.
  /// BE (Orders/checkout) đọc giỏ hàng từ server nên phải đẩy item lên trước.
  static Future<CheckoutResult> checkout(
      List<CartItem> items, String address) async {
    final headers = await _headers();
    final base = AppConstants.baseUrl;

    // 1. Làm sạch giỏ hàng server (tránh trùng từ lần trước)
    await http.delete(Uri.parse('$base/cart/clear'), headers: headers);

    // 2. Đẩy từng item lên giỏ hàng server
    for (final i in items) {
      await http.post(
        Uri.parse('$base/cart'),
        headers: headers,
        body: jsonEncode({
          'productId': i.product.id,
          'quantity': i.quantity,
          'size': i.size,
        }),
      );
    }

    // 3. Checkout — body là chuỗi JSON của địa chỉ (BE nhận [FromBody] string)
    final res = await http.post(
      Uri.parse('$base/orders/checkout'),
      headers: headers,
      body: jsonEncode(address),
    );

    if (res.statusCode == 200) {
      final data = jsonDecode(res.body);
      return CheckoutResult(
        orderId: data['orderId']?.toString() ?? '',
        checkoutUrl: data['checkoutUrl']?.toString() ?? '',
      );
    }

    String msg = 'Đặt hàng thất bại, vui lòng thử lại';
    try {
      msg = jsonDecode(res.body)['message'] ?? msg;
    } catch (_) {}
    throw Exception(msg);
  }

  /// Lấy danh sách đơn hàng của user đang đăng nhập.
  static Future<List<OrderModel>> getMyOrders() async {
    final headers = await _headers();
    final res = await http.get(
      Uri.parse('${AppConstants.baseUrl}/orders/my'),
      headers: headers,
    );
    if (res.statusCode == 200) {
      final List data = jsonDecode(res.body);
      return data.map((e) => OrderModel.fromJson(e)).toList();
    }
    return [];
  }
}
