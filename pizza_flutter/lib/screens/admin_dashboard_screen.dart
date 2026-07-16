import 'package:flutter/material.dart';
import '../core/order_status.dart';
import '../models/dashboard.dart';
import '../services/dashboard_service.dart';

class AdminDashboardScreen extends StatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  State<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends State<AdminDashboardScreen> {
  DashboardData? _data;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final d = await DashboardService.load();
    if (mounted) {
      setState(() {
        _data = d;
        _loading = false;
      });
    }
  }

  String _money(double v) {
    final s = v.toStringAsFixed(0);
    final buf = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) buf.write('.');
      buf.write(s[i]);
    }
    buf.write('đ');
    return buf.toString();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Bảng điều khiển',
            style: TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: _loading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : RefreshIndicator(
              onRefresh: _load,
              color: const Color(0xFFD85A30),
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (_data!.allFailed) _errorBanner(),
                  _revenueCards(),
                  const SizedBox(height: 16),
                  _statusCard(),
                  const SizedBox(height: 16),
                  _topProductsCard(),
                  const SizedBox(height: 16),
                  _systemCard(),
                ],
              ),
            ),
    );
  }

  Widget _errorBanner() => Container(
        margin: const EdgeInsets.only(bottom: 16),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFFDECEA),
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Row(children: [
          Icon(Icons.wifi_off, color: Colors.red, size: 18),
          SizedBox(width: 8),
          Expanded(
              child: Text('Không tải được dữ liệu. Kéo xuống để thử lại.',
                  style: TextStyle(fontSize: 13, color: Colors.red))),
        ]),
      );

  Widget _card({required String title, required Widget child}) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title,
              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
          const SizedBox(height: 12),
          child,
        ]),
      );

  Widget _statTile(String label, String value, IconData icon) => Expanded(
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
          ),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Icon(icon, color: const Color(0xFFD85A30), size: 20),
            const SizedBox(height: 8),
            Text(value,
                style:
                    const TextStyle(fontWeight: FontWeight.w900, fontSize: 17)),
            const SizedBox(height: 2),
            Text(label, style: const TextStyle(fontSize: 11, color: Colors.grey)),
          ]),
        ),
      );

  Widget _revenueCards() {
    final o = _data!.orders;
    return Column(children: [
      Row(children: [
        _statTile('Doanh thu hôm nay',
            o == null ? '—' : _money(o.revenueToday), Icons.today),
        const SizedBox(width: 10),
        _statTile('Doanh thu tổng',
            o == null ? '—' : _money(o.revenueTotal), Icons.payments),
      ]),
      const SizedBox(height: 10),
      Row(children: [
        _statTile('Đơn hôm nay', o == null ? '—' : '${o.ordersToday}',
            Icons.receipt_long),
        const SizedBox(width: 10),
        _statTile(
            'Tổng đơn', o == null ? '—' : '${o.ordersTotal}', Icons.list_alt),
      ]),
    ]);
  }

  Widget _statusCard() {
    final o = _data!.orders;
    if (o == null) {
      return _card(
          title: 'Đơn theo trạng thái',
          child: const Text('— Không tải được',
              style: TextStyle(color: Colors.grey, fontSize: 13)));
    }
    return _card(
      title: 'Đơn theo trạng thái',
      child: Column(
        children: o.byStatus.entries.map((e) {
          final color = orderStatusColorByName(e.key);
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 5),
            child: Row(children: [
              Container(
                  width: 10,
                  height: 10,
                  decoration:
                      BoxDecoration(color: color, shape: BoxShape.circle)),
              const SizedBox(width: 10),
              Expanded(
                  child: Text(orderStatusLabelByName(e.key),
                      style: const TextStyle(fontSize: 13))),
              Text('${e.value}',
                  style: const TextStyle(
                      fontWeight: FontWeight.w800, fontSize: 13)),
            ]),
          );
        }).toList(),
      ),
    );
  }

  Widget _topProductsCard() {
    final o = _data!.orders;
    if (o == null || o.topProducts.isEmpty) {
      return _card(
          title: 'Món bán chạy',
          child: Text(
              o == null ? '— Không tải được' : 'Chưa có đơn đã thanh toán',
              style: const TextStyle(color: Colors.grey, fontSize: 13)));
    }
    return _card(
      title: 'Top món bán chạy',
      child: Column(
        children: o.topProducts.asMap().entries.map((e) {
          final p = e.value;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 5),
            child: Row(children: [
              Container(
                width: 22,
                height: 22,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                    color: const Color(0xFFFAECE7),
                    borderRadius: BorderRadius.circular(6)),
                child: Text('${e.key + 1}',
                    style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFFD85A30))),
              ),
              const SizedBox(width: 10),
              Expanded(
                  child: Text(p.productName,
                      style: const TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w600))),
              Text('x${p.quantity}',
                  style: const TextStyle(fontSize: 12, color: Colors.grey)),
              const SizedBox(width: 10),
              Text(_money(p.revenue),
                  style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFFD85A30))),
            ]),
          );
        }).toList(),
      ),
    );
  }

  Widget _row(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(children: [
          Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
          Text(value,
              style:
                  const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
        ]),
      );

  Widget _systemCard() {
    final a = _data!.auth;
    final p = _data!.products;
    final c = _data!.categories;
    return _card(
      title: 'Tổng quan hệ thống',
      child: Column(children: [
        _row('Tổng người dùng', a == null ? '—' : '${a.totalUsers}'),
        if (a != null)
          ...a.byRole.entries.map((e) => Padding(
                padding: const EdgeInsets.only(left: 12),
                child: _row('· ${e.key}', '${e.value}'),
              )),
        const Divider(height: 18),
        _row('Sản phẩm', p == null ? '—' : '${p.totalProducts}'),
        if (p != null) ...[
          Padding(
              padding: const EdgeInsets.only(left: 12),
              child: _row('· Còn bán', '${p.available}')),
          Padding(
              padding: const EdgeInsets.only(left: 12),
              child: _row('· Ngừng bán', '${p.unavailable}')),
        ],
        const Divider(height: 18),
        _row('Danh mục', c == null ? '—' : '${c.totalCategories}'),
      ]),
    );
  }
}
