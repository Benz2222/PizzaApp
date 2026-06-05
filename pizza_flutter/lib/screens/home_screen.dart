import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../models/product.dart';
import '../services/product_service.dart';
import '../services/auth_service.dart';
import '../providers/cart_provider.dart';
import 'detail_screen.dart';
import 'cart_screen.dart';
import 'login_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  List<Product> _products = [];
  List<Product> _filtered = [];
  String _selectedCategory = 'Tất cả';
  bool _isLoading = true;

  final List<String> _categories = [
    'Tất cả', 'Truyền thống', 'Hải sản', 'Chay', 'Đặc biệt'
  ];

  final Map<String, String> _categoryEmoji = {
    'Truyền thống': '🍕',
    'Hải sản': '🦐',
    'Chay': '🥦',
    'Đặc biệt': '⭐',
  };

  @override
  void initState() {
    super.initState();
    _loadProducts();
  }

  Future<void> _loadProducts() async {
    final products = await ProductService.getAll();
    setState(() {
      _products = products;
      _filtered = products;
      _isLoading = false;
    });
  }

  void _filterCategory(String cat) {
    setState(() {
      _selectedCategory = cat;
      _filtered = cat == 'Tất cả'
          ? _products
          : _products.where((p) => p.category == cat).toList();
    });
  }

  @override
  Widget build(BuildContext context) {
    final cart = context.watch<CartProvider>();
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      body: Column(
        children: [
          _buildHeader(cart),
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator(
                color: Color(0xFFD85A30)))
                : RefreshIndicator(
              onRefresh: _loadProducts,
              color: const Color(0xFFD85A30),
              child: SingleChildScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildPromoBanner(),
                    const SizedBox(height: 16),
                    _buildCategories(),
                    const SizedBox(height: 16),
                    const Text('Phổ biến nhất',
                        style: TextStyle(fontSize: 16,
                            fontWeight: FontWeight.w800)),
                    const SizedBox(height: 12),
                    _buildGrid(),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: _buildBottomNav(cart),
    );
  }

  Widget _buildHeader(CartProvider cart) {
    return Container(
      color: const Color(0xFFD85A30),
      padding: EdgeInsets.only(
          top: MediaQuery.of(context).padding.top + 12,
          left: 16, right: 16, bottom: 16),
      child: Row(
        children: [
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Xin chào 👋',
                    style: TextStyle(color: Colors.white70, fontSize: 13)),
                Text('Hôm nay ăn gì nào?',
                    style: TextStyle(color: Colors.white,
                        fontSize: 18, fontWeight: FontWeight.w800)),
              ],
            ),
          ),
          GestureDetector(
            onTap: () async {
              await AuthService.logout();
              if (mounted) {
                Navigator.pushAndRemoveUntil(context,
                    MaterialPageRoute(builder: (_) => const LoginScreen()),
                        (route) => false);
              }
            },
            child: const CircleAvatar(
                backgroundColor: Colors.white24,
                child: Icon(Icons.person, color: Colors.white)),
          ),
        ],
      ),
    );
  }

  Widget _buildPromoBanner() {
    return Container(
      decoration: BoxDecoration(
        gradient: const LinearGradient(
            colors: [Color(0xFFD85A30), Color(0xFF993C1D)]),
        borderRadius: BorderRadius.circular(16),
      ),
      padding: const EdgeInsets.all(16),
      child: const Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Giảm 30%', style: TextStyle(fontSize: 22,
                    fontWeight: FontWeight.w900, color: Colors.white)),
                Text('Đơn đầu tiên của bạn!',
                    style: TextStyle(color: Colors.white70, fontSize: 13)),
              ],
            ),
          ),
          Text('🍕', style: TextStyle(fontSize: 48)),
        ],
      ),
    );
  }

  Widget _buildCategories() {
    return SizedBox(
      height: 36,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: _categories.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (_, i) {
          final cat = _categories[i];
          final active = cat == _selectedCategory;
          return GestureDetector(
            onTap: () => _filterCategory(cat),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(
                color: active ? const Color(0xFFD85A30) : Colors.white,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                    color: active
                        ? const Color(0xFFD85A30)
                        : const Color(0xFFD3D1C7)),
              ),
              alignment: Alignment.center,
              child: Text(cat,
                  style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: active ? Colors.white : Colors.grey)),
            ),
          );
        },
      ),
    );
  }

  Widget _buildGrid() {
    if (_filtered.isEmpty) {
      return const Center(
          child: Padding(
              padding: EdgeInsets.all(32),
              child: Text('Không có sản phẩm',
                  style: TextStyle(color: Colors.grey))));
    }
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        crossAxisSpacing: 10,
        mainAxisSpacing: 10,
        childAspectRatio: 0.78,
      ),
      itemCount: _filtered.length,
      itemBuilder: (_, i) => _buildProductCard(_filtered[i]),
    );
  }

  Widget _buildProductCard(Product product) {
    final cart = context.read<CartProvider>();
    final emoji = _categoryEmoji[product.category] ?? '🍕';
    return GestureDetector(
      onTap: () => Navigator.push(context,
          MaterialPageRoute(builder: (_) => DetailScreen(product: product))),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              height: 100,
              decoration: const BoxDecoration(
                color: Color(0xFFFAECE7),
                borderRadius: BorderRadius.vertical(top: Radius.circular(14)),
              ),
              alignment: Alignment.center,
              child: Text(emoji, style: const TextStyle(fontSize: 48)),
            ),
            Padding(
              padding: const EdgeInsets.all(10),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(product.name,
                      style: const TextStyle(fontSize: 13,
                          fontWeight: FontWeight.w800),
                      maxLines: 1, overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 2),
                  Text(product.description,
                      style: const TextStyle(fontSize: 11, color: Colors.grey),
                      maxLines: 2, overflow: TextOverflow.ellipsis),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                          '${product.price.toStringAsFixed(0)}đ',
                          style: const TextStyle(fontSize: 13,
                              fontWeight: FontWeight.w800,
                              color: Color(0xFFD85A30))),
                      GestureDetector(
                        onTap: () {
                          cart.addItem(product);
                          ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text('Đã thêm ${product.name}!'),
                                duration: const Duration(seconds: 1),
                                backgroundColor: const Color(0xFFD85A30),
                              ));
                        },
                        child: Container(
                          width: 28, height: 28,
                          decoration: BoxDecoration(
                            color: const Color(0xFFD85A30),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Icon(Icons.add,
                              color: Colors.white, size: 18),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBottomNav(CartProvider cart) {
    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: Color(0xFFD3D1C7), width: 0.5)),
      ),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Row(
            children: [
              _navItem(Icons.home, 'Trang chủ', true, null),
              _navItem(Icons.shopping_cart_outlined, 'Giỏ hàng', false,
                      () => Navigator.push(context,
                      MaterialPageRoute(builder: (_) => const CartScreen())),
                  badge: cart.totalCount),
              _navItem(Icons.person_outline, 'Tài khoản', false, () async {
                await AuthService.logout();
                if (mounted) {
                  Navigator.pushAndRemoveUntil(context,
                      MaterialPageRoute(builder: (_) => const LoginScreen()),
                          (route) => false);
                }
              }),
            ],
          ),
        ),
      ),
    );
  }

  Widget _navItem(IconData icon, String label, bool active,
      VoidCallback? onTap, {int badge = 0}) {
    return Expanded(
      child: GestureDetector(
        onTap: onTap,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                Icon(icon,
                    color: active
                        ? const Color(0xFFD85A30)
                        : const Color(0xFFB4B2A9),
                    size: 24),
                if (badge > 0)
                  Positioned(
                    top: -4, right: -6,
                    child: Container(
                      width: 16, height: 16,
                      decoration: const BoxDecoration(
                          color: Color(0xFFD85A30),
                          shape: BoxShape.circle),
                      alignment: Alignment.center,
                      child: Text('$badge',
                          style: const TextStyle(color: Colors.white,
                              fontSize: 10, fontWeight: FontWeight.w800)),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 2),
            Text(label,
                style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: active
                        ? const Color(0xFFD85A30)
                        : const Color(0xFFB4B2A9))),
          ],
        ),
      ),
    );
  }
}