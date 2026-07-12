import 'dart:async';
import 'package:flutter/material.dart';
import '../models/order.dart';
import '../core/order_status.dart';
import '../services/order_service.dart';

class AdminOrdersScreen extends StatefulWidget {
  const AdminOrdersScreen({super.key});

  @override
  State<AdminOrdersScreen> createState() => _AdminOrdersScreenState();
}

class _AdminOrdersScreenState extends State<AdminOrdersScreen> {
  List<OrderModel> _orders = [];
  bool _isLoading = true;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 8), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) setState(() => _isLoading = true);
    final orders = await OrderService.getAllOrders();
    if (mounted) {
      setState(() {
        _orders = orders;
        _isLoading = false;
      });
    }
  }

  Future<void> _act(Future<String?> Function() action) async {
    final err = await action();
    if (!mounted) return;
    if (err == null) {
      _load(silent: true);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(err), backgroundColor: Colors.red));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Quản lý đơn hàng',
            style: TextStyle(fontWeight: FontWeight.w800)),
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : _orders.isEmpty
              ? const Center(child: Text('Chưa có đơn hàng',
                  style: TextStyle(color: Colors.grey)))
              : RefreshIndicator(
                  onRefresh: _load,
                  color: const Color(0xFFD85A30),
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _orders.length,
                    itemBuilder: (_, i) => _buildCard(_orders[i]),
                  ),
                ),
    );
  }

  Widget _buildCard(OrderModel o) {
    final shortId = o.id.length <= 6
        ? o.id.toUpperCase()
        : o.id.substring(o.id.length - 6).toUpperCase();
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('#PZL-$shortId',
                  style: const TextStyle(fontWeight: FontWeight.w800)),
              orderStatusBadge(o),
            ],
          ),
          const SizedBox(height: 6),
          Row(children: [
            const Icon(Icons.location_on, size: 14, color: Colors.grey),
            const SizedBox(width: 4),
            Expanded(child: Text(o.deliveryAddress,
                style: const TextStyle(fontSize: 12, color: Colors.grey),
                maxLines: 1, overflow: TextOverflow.ellipsis)),
            Text('${o.totalPrice.toStringAsFixed(0)}đ',
                style: const TextStyle(fontWeight: FontWeight.w800,
                    color: Color(0xFFD85A30))),
          ]),
          const Divider(height: 16),
          ...o.items.map((it) => Text('• ${it.productName} · Size ${it.size} x${it.quantity}',
              style: const TextStyle(fontSize: 13))),
          _buildAction(o),
        ],
      ),
    );
  }

  Widget _buildAction(OrderModel o) {
    // Nút hành động tùy trạng thái
    if (o.isUnpaid) {
      return _btn('XÁC NHẬN THANH TOÁN', const Color(0xFF8E44AD),
          () => _act(() => OrderService.confirmPayment(o.id)));
    }
    switch (o.status) {
      case 'Paid':
        return _btn('BẮT ĐẦU CHUẨN BỊ', const Color(0xFFD85A30),
            () => _act(() => OrderService.adminUpdateStatus(o.id, 'Preparing')));
      case 'Preparing':
        return _btn('XONG MÓN · CHỜ GIAO', const Color(0xFFBA7517),
            () => _act(() => OrderService.adminUpdateStatus(o.id, 'Ready')));
      case 'Ready':
        return _hint('Chờ shipper nhận đơn...');
      case 'Delivering':
        return _hint('Shipper đang giao...');
      default:
        return const SizedBox.shrink();
    }
  }

  Widget _btn(String label, Color color, VoidCallback onTap) {
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: SizedBox(
        width: double.infinity,
        height: 42,
        child: ElevatedButton(
          onPressed: onTap,
          style: ElevatedButton.styleFrom(
            backgroundColor: color,
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10)),
          ),
          child: Text(label,
              style: const TextStyle(color: Colors.white,
                  fontWeight: FontWeight.w800, letterSpacing: 0.5)),
        ),
      ),
    );
  }

  Widget _hint(String text) => Padding(
        padding: const EdgeInsets.only(top: 10),
        child: Text(text,
            style: const TextStyle(
                fontSize: 12, fontStyle: FontStyle.italic, color: Colors.grey)),
      );
}
