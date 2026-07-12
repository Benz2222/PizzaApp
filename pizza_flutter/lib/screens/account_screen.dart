import 'package:flutter/material.dart';
import '../services/auth_service.dart';
import 'login_screen.dart';
import 'admin_products_screen.dart';
import 'admin_categories_screen.dart';
import 'shipper_screen.dart';

class AccountScreen extends StatefulWidget {
  const AccountScreen({super.key});

  @override
  State<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends State<AccountScreen> {
  Map<String, dynamic>? _user;
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final user = await AuthService.getMe();
    if (mounted) {
      setState(() {
        _user = user;
        _isLoading = false;
      });
    }
  }

  Future<void> _logout() async {
    await AuthService.logout();
    if (mounted) {
      Navigator.pushAndRemoveUntil(context,
          MaterialPageRoute(builder: (_) => const LoginScreen()),
          (route) => false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final name = _user?['fullName'] ?? '';
    final role = (_user?['role'] ?? 'Customer').toString();

    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Tài khoản',
            style: TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : _user == null
              ? _buildError()
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    const SizedBox(height: 8),
                    Center(
                      child: CircleAvatar(
                        radius: 44,
                        backgroundColor: const Color(0xFFFAECE7),
                        child: Text(
                          name.isNotEmpty ? name[0].toUpperCase() : '?',
                          style: const TextStyle(
                              fontSize: 40,
                              fontWeight: FontWeight.w900,
                              color: Color(0xFFD85A30)),
                        ),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Center(
                      child: Text(name,
                          style: const TextStyle(
                              fontSize: 20, fontWeight: FontWeight.w800)),
                    ),
                    const SizedBox(height: 4),
                    Center(child: _roleBadge(role)),
                    const SizedBox(height: 24),
                    _infoTile(Icons.email_outlined, 'Email',
                        _user?['email'] ?? ''),
                    _infoTile(Icons.phone_outlined, 'Số điện thoại',
                        _user?['phoneNumber'] ?? ''),
                    _infoTile(Icons.location_on_outlined, 'Địa chỉ',
                        (_user?['address'] ?? '').toString().isEmpty
                            ? 'Chưa cập nhật'
                            : _user!['address']),
                    const SizedBox(height: 12),

                    // Khu vực quản trị theo vai trò
                    if (role == 'Admin') ...[
                      _menuTile(Icons.fastfood_outlined, 'Quản lý sản phẩm',
                          () => Navigator.push(context, MaterialPageRoute(
                              builder: (_) => const AdminProductsScreen()))),
                      _menuTile(Icons.category_outlined, 'Quản lý danh mục',
                          () => Navigator.push(context, MaterialPageRoute(
                              builder: (_) => const AdminCategoriesScreen()))),
                    ],
                    if (role == 'Admin' || role == 'Shipper')
                      _menuTile(Icons.delivery_dining_outlined, 'Đơn cần giao',
                          () => Navigator.push(context, MaterialPageRoute(
                              builder: (_) => const ShipperScreen()))),

                    const SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      height: 50,
                      child: OutlinedButton.icon(
                        onPressed: _logout,
                        icon: const Icon(Icons.logout, color: Colors.red),
                        label: const Text('ĐĂNG XUẤT',
                            style: TextStyle(color: Colors.red,
                                fontWeight: FontWeight.w800, letterSpacing: 1)),
                        style: OutlinedButton.styleFrom(
                          side: const BorderSide(color: Colors.red),
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(14)),
                        ),
                      ),
                    ),
                  ],
                ),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Text('Không tải được thông tin tài khoản',
              style: TextStyle(color: Colors.grey)),
          const SizedBox(height: 12),
          TextButton(onPressed: _load, child: const Text('Thử lại')),
          TextButton(onPressed: _logout, child: const Text('Đăng xuất')),
        ],
      ),
    );
  }

  Widget _roleBadge(String role) {
    final isAdmin = role == 'Admin';
    final isShipper = role == 'Shipper';
    final color = isAdmin
        ? const Color(0xFFD85A30)
        : isShipper
            ? const Color(0xFF2D7DD2)
            : const Color(0xFF639922);
    final label = isAdmin
        ? 'Quản trị viên'
        : isShipper
            ? 'Shipper'
            : 'Khách hàng';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label,
          style: TextStyle(
              fontSize: 12, fontWeight: FontWeight.w700, color: color)),
    );
  }

  Widget _menuTile(IconData icon, String label, VoidCallback onTap) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: ListTile(
        leading: Icon(icon, color: const Color(0xFFD85A30)),
        title: Text(label, style: const TextStyle(fontWeight: FontWeight.w700)),
        trailing: const Icon(Icons.chevron_right, color: Colors.grey),
        onTap: onTap,
      ),
    );
  }

  Widget _infoTile(IconData icon, String label, String value) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Row(
        children: [
          Icon(icon, color: const Color(0xFFD85A30), size: 20),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label.toUpperCase(),
                  style: const TextStyle(fontSize: 10,
                      fontWeight: FontWeight.w700,
                      color: Colors.grey, letterSpacing: 1)),
              const SizedBox(height: 2),
              Text(value,
                  style: const TextStyle(fontSize: 14,
                      fontWeight: FontWeight.w600)),
            ],
          ),
        ],
      ),
    );
  }
}
