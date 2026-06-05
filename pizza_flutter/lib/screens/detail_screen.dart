import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../models/product.dart';
import '../providers/cart_provider.dart';
import 'cart_screen.dart';

class DetailScreen extends StatefulWidget {
  final Product product;
  const DetailScreen({super.key, required this.product});

  @override
  State<DetailScreen> createState() => _DetailScreenState();
}

class _DetailScreenState extends State<DetailScreen> {
  String _selectedSize = 'M';
  int _qty = 1;

  final Map<String, double> _sizeMultiplier = {
    'S': 0.85, 'M': 1.0, 'L': 1.2
  };

  double get _totalPrice =>
      widget.product.price * (_sizeMultiplier[_selectedSize] ?? 1.0) * _qty;

  final Map<String, String> _categoryEmoji = {
    'Truyền thống': '🍕', 'Hải sản': '🦐', 'Chay': '🥦', 'Đặc biệt': '⭐',
  };

  @override
  Widget build(BuildContext context) {
    final cart = context.read<CartProvider>();
    final emoji = _categoryEmoji[widget.product.category] ?? '🍕';

    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      body: Column(
        children: [
          Stack(
            children: [
              Container(
                height: 260,
                color: const Color(0xFFFAECE7),
                alignment: Alignment.center,
                child: Text(emoji,
                    style: const TextStyle(fontSize: 100)),
              ),
              Positioned(
                top: MediaQuery.of(context).padding.top + 8,
                left: 12,
                child: GestureDetector(
                  onTap: () => Navigator.pop(context),
                  child: Container(
                      width: 36, height: 36,
                      decoration: BoxDecoration(
                        color: Colors.white.withOpacity(0.9),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Icon(Icons.arrow_back_ios_new, size: 18)),
                ),
              ),
              Positioned(
                top: MediaQuery.of(context).padding.top + 8,
                right: 12,
                child: GestureDetector(
                  onTap: () => Navigator.push(context,
                      MaterialPageRoute(builder: (_) => const CartScreen())),
                  child: Container(
                      width: 36, height: 36,
                      decoration: BoxDecoration(
                        color: const Color(0xFFD85A30),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Icon(Icons.shopping_cart_outlined,
                          color: Colors.white, size: 18)),
                ),
              ),
            ],
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                          child: Text(widget.product.name,
                              style: const TextStyle(fontSize: 22,
                                  fontWeight: FontWeight.w800))),
                      Text(
                          '${_totalPrice.toStringAsFixed(0)}đ',
                          style: const TextStyle(fontSize: 20,
                              fontWeight: FontWeight.w800,
                              color: Color(0xFFD85A30))),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(children: [
                    const Icon(Icons.star, color: Color(0xFFBA7517), size: 16),
                    const SizedBox(width: 4),
                    const Text('4.8',
                        style: TextStyle(fontWeight: FontWeight.w700,
                            color: Color(0xFFBA7517))),
                    const SizedBox(width: 8),
                    Text('200+ đánh giá',
                        style: TextStyle(fontSize: 12,
                            color: Colors.grey[600])),
                  ]),
                  const SizedBox(height: 12),
                  Text(widget.product.description,
                      style: TextStyle(fontSize: 14,
                          color: Colors.grey[600], height: 1.6)),
                  const SizedBox(height: 20),
                  const Text('Chọn kích cỡ',
                      style: TextStyle(fontSize: 15,
                          fontWeight: FontWeight.w800)),
                  const SizedBox(height: 10),
                  Row(
                    children: ['S', 'M', 'L'].map((size) {
                      final active = size == _selectedSize;
                      return GestureDetector(
                        onTap: () => setState(() => _selectedSize = size),
                        child: Container(
                          margin: const EdgeInsets.only(right: 10),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 20, vertical: 8),
                          decoration: BoxDecoration(
                            color: active
                                ? const Color(0xFFFAECE7)
                                : Colors.white,
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(
                                color: active
                                    ? const Color(0xFFD85A30)
                                    : const Color(0xFFD3D1C7),
                                width: active ? 1.5 : 0.5),
                          ),
                          child: Text(size,
                              style: TextStyle(
                                  fontWeight: FontWeight.w700,
                                  color: active
                                      ? const Color(0xFFD85A30)
                                      : Colors.grey)),
                        ),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 20),
                  Row(
                    children: [
                      const Text('Số lượng',
                          style: TextStyle(fontSize: 15,
                              fontWeight: FontWeight.w800)),
                      const Spacer(),
                      _qtyButton(Icons.remove,
                              () => setState(() { if (_qty > 1) _qty--; })),
                      Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 16),
                          child: Text('$_qty',
                              style: const TextStyle(fontSize: 18,
                                  fontWeight: FontWeight.w800))),
                      _qtyButton(Icons.add,
                              () => setState(() => _qty++)),
                    ],
                  ),
                  const SizedBox(height: 24),
                  SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: ElevatedButton.icon(
                      onPressed: () {
                        for (int i = 0; i < _qty; i++) {
                          cart.addItem(widget.product, size: _selectedSize);
                        }
                        ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Đã thêm vào giỏ hàng!'),
                              backgroundColor: Color(0xFFD85A30),
                              duration: Duration(seconds: 1),
                            ));
                        Navigator.pop(context);
                      },
                      icon: const Icon(Icons.shopping_cart_outlined,
                          color: Colors.white),
                      label: const Text('THÊM VÀO GIỎ HÀNG',
                          style: TextStyle(color: Colors.white,
                              fontWeight: FontWeight.w800, letterSpacing: 1)),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFFD85A30),
                        shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14)),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _qtyButton(IconData icon, VoidCallback onTap) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
          width: 34, height: 34,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: const Color(0xFFD3D1C7)),
          ),
          child: Icon(icon, size: 18)),
    );
  }
}