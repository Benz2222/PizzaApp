import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../providers/cart_provider.dart';
import '../services/order_service.dart';
import '../widgets/product_image.dart';
import 'order_success_screen.dart';

class CartScreen extends StatefulWidget {
  const CartScreen({super.key});

  @override
  State<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends State<CartScreen> {
  final _addressController = TextEditingController(
      text: '123 Nguyễn Huệ, Quận 1, TP.HCM');
  bool _isOrdering = false;

  Future<void> _placeOrder(CartProvider cart) async {
    if (cart.items.isEmpty) return;
    setState(() => _isOrdering = true);

    try {
      final result = await OrderService.checkout(
        cart.items,
        _addressController.text.trim(),
      );
      cart.clear();

      // Không tự mở trình duyệt nữa — hiện QR ngay trong app,
      // người dùng quét bằng app ngân hàng hoặc bấm nút mở trang thanh toán.
      if (mounted) {
        Navigator.pushAndRemoveUntil(
          context,
          MaterialPageRoute(
            builder: (_) => OrderSuccessScreen(
              orderId: result.orderId,
              paymentUrl: result.checkoutUrl,
              paymentQr: result.qrCode,
            ),
          ),
          (route) => route.isFirst,
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(e.toString().replaceFirst('Exception: ', '')),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
    if (mounted) setState(() => _isOrdering = false);
  }

  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartProvider>();
    final discount = cart.totalPrice * 0.3;
    final shipping = cart.items.isEmpty ? 0.0 : 20000.0;
    final total = cart.totalPrice - discount + shipping;

    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Giỏ hàng',
            style: TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: cart.items.isEmpty
          ? const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text('🛒', style: TextStyle(fontSize: 64)),
              SizedBox(height: 12),
              Text('Giỏ hàng trống',
                  style: TextStyle(fontSize: 16, color: Colors.grey,
                      fontWeight: FontWeight.w600)),
            ],
          ))
          : Column(
        children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                ...cart.items.map((item) => _buildCartItem(item, cart)),
                const SizedBox(height: 12),
                _buildAddressField(),
                const SizedBox(height: 12),
                _buildSummary(cart.totalPrice, discount, shipping, total),
              ],
            ),
          ),
          _buildCheckoutButton(cart, total),
        ],
      ),
    );
  }

  Widget _buildCartItem(CartItem item, CartProvider cart) {
    final Map<String, String> emoji = {
      'Truyền thống': '🍕', 'Hải sản': '🦐', 'Chay': '🥦', 'Đặc biệt': '⭐',
    };
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Row(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: Container(
              width: 56, height: 56,
              color: const Color(0xFFFAECE7),
              child: ProductImage(
                  imageUrl: item.product.imageUrl,
                  emoji: emoji[item.product.category] ?? '🍕',
                  emojiSize: 30),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(item.product.name,
                    style: const TextStyle(fontWeight: FontWeight.w800,
                        fontSize: 14)),
                Text('Size ${item.size} · x${item.quantity}',
                    style: const TextStyle(fontSize: 12, color: Colors.grey)),
                const SizedBox(height: 4),
                Text('${item.subtotal.toStringAsFixed(0)}đ',
                    style: const TextStyle(fontWeight: FontWeight.w800,
                        color: Color(0xFFD85A30))),
              ],
            ),
          ),
          IconButton(
              onPressed: () =>
                  cart.removeItem(item.product.id, item.size),
              icon: const Icon(Icons.close, color: Colors.red, size: 20)),
        ],
      ),
    );
  }

  Widget _buildAddressField() {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(children: [
            Icon(Icons.location_on, color: Color(0xFFD85A30), size: 18),
            SizedBox(width: 6),
            Text('Địa chỉ giao hàng',
                style: TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
          ]),
          const SizedBox(height: 8),
          TextField(
            controller: _addressController,
            style: const TextStyle(fontSize: 13),
            decoration: InputDecoration(
              border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
              enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(8),
                  borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
              contentPadding: const EdgeInsets.symmetric(
                  horizontal: 12, vertical: 10),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSummary(double subtotal, double discount,
      double shipping, double total) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Column(children: [
        _sumRow('Tạm tính', '${subtotal.toStringAsFixed(0)}đ'),
        _sumRow('Phí giao hàng', '${shipping.toStringAsFixed(0)}đ'),
        _sumRow('Giảm giá (30%)',
            '-${discount.toStringAsFixed(0)}đ', green: true),
        const Divider(height: 20),
        _sumRow('Tổng cộng', '${total.toStringAsFixed(0)}đ',
            bold: true, red: true),
      ]),
    );
  }

  Widget _sumRow(String label, String value,
      {bool bold = false, bool red = false, bool green = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label,
              style: TextStyle(
                  fontSize: bold ? 15 : 13,
                  fontWeight: bold ? FontWeight.w800 : FontWeight.normal,
                  color: Colors.grey[700])),
          Text(value,
              style: TextStyle(
                  fontSize: bold ? 15 : 13,
                  fontWeight: bold ? FontWeight.w800 : FontWeight.w600,
                  color: red
                      ? const Color(0xFFD85A30)
                      : green
                      ? const Color(0xFF639922)
                      : Colors.black87)),
        ],
      ),
    );
  }

  Widget _buildCheckoutButton(CartProvider cart, double total) {
    return Container(
      color: Colors.white,
      padding: EdgeInsets.fromLTRB(16, 12, 16,
          MediaQuery.of(context).padding.bottom + 12),
      child: SizedBox(
        width: double.infinity,
        height: 50,
        child: ElevatedButton(
          onPressed: _isOrdering ? null : () => _placeOrder(cart),
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFFD85A30),
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(14)),
          ),
          child: _isOrdering
              ? const CircularProgressIndicator(color: Colors.white)
              : Text('ĐẶT HÀNG · ${total.toStringAsFixed(0)}đ',
              style: const TextStyle(color: Colors.white,
                  fontWeight: FontWeight.w800, letterSpacing: 0.5)),
        ),
      ),
    );
  }
}