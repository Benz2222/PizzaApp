import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../models/product.dart';
import '../services/product_service.dart';
import '../services/category_service.dart';
import '../core/text_utils.dart';
import '../providers/cart_provider.dart';
import '../widgets/product_image.dart';
import 'detail_screen.dart';
import 'cart_screen.dart';
import 'orders_screen.dart';
import 'account_screen.dart';

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

  // Danh mục tải động từ BE (luôn có 'Tất cả' đứng đầu)
  List<String> _categories = ['Tất cả'];

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
    _loadCategories();
  }

  Future<void> _loadProducts() async {
    final products = await ProductService.getAll();
    if (!mounted) return;
    setState(() {
      _products = products;
      _filtered = _selectedCategory == 'Tất cả'
          ? products
          : products.where((p) => p.category == _selectedCategory).toList();
      _isLoading = false;
    });
  }

  Future<void> _loadCategories() async {
    final names = await CategoryService.getNames();
    if (!mounted || names.isEmpty) return;
    setState(() => _categories = ['Tất cả', ...names]);
  }

  String _searchQuery = '';

  void _filterCategory(String cat) {
    _selectedCategory = cat;
    _applyFilters();
  }

  void _applyFilters() {
    var list = _products;
    if (_selectedCategory != 'Tất cả') {
      list = list.where((p) => p.category == _selectedCategory).toList();
    }
    final q = removeDiacritics(_searchQuery.trim());
    if (q.isNotEmpty) {
      list = list.where((p) =>
          removeDiacritics(p.name).contains(q) ||
          removeDiacritics(p.description).contains(q)).toList();
    }
    setState(() => _filtered = list);
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
                    _buildSearchBar(),
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
            onTap: () => Navigator.push(context,
                MaterialPageRoute(builder: (_) => const AccountScreen())),
            child: const CircleAvatar(
                backgroundColor: Colors.white24,
                child: Icon(Icons.person, color: Colors.white)),
          ),
        ],
      ),
    );
  }

  Widget _buildSearchBar() {
    return TextField(
      onChanged: (v) {
        _searchQuery = v;
        _applyFilters();
      },
      decoration: InputDecoration(
        hintText: 'Tìm pizza theo tên...',
        hintStyle: const TextStyle(color: Colors.grey, fontSize: 14),
        prefixIcon: const Icon(Icons.search, color: Color(0xFFD85A30)),
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(vertical: 4),
        border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
        enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
        focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFD85A30))),
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
            ClipRRect(
              borderRadius: const BorderRadius.vertical(top: Radius.circular(14)),
              child: Container(
                height: 100,
                color: const Color(0xFFFAECE7),
                child: ProductImage(
                    imageUrl: product.imageUrl, emoji: emoji, emojiSize: 48),
              ),
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
              _navItem(Icons.receipt_long_outlined, 'Đơn hàng', false,
                      () => Navigator.push(context,
                      MaterialPageRoute(builder: (_) => const OrdersScreen()))),
              _navItem(Icons.person_outline, 'Tài khoản', false,
                      () => Navigator.push(context,
                      MaterialPageRoute(builder: (_) => const AccountScreen()))),
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