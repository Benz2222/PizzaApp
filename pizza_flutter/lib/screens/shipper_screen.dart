import 'package:flutter/material.dart';
import '../models/order.dart';
import '../services/order_service.dart';

class ShipperScreen extends StatefulWidget {
  const ShipperScreen({super.key});

  @override
  State<ShipperScreen> createState() => _ShipperScreenState();
}

class _ShipperScreenState extends State<ShipperScreen> {
  List<OrderModel> _orders = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    final orders = await OrderService.getShipperOrders();
    if (mounted) {
      setState(() {
        _orders = orders;
        _isLoading = false;
      });
    }
  }

  Future<void> _setStatus(OrderModel order, String status) async {
    final err = await OrderService.updateStatus(order.id, status);
    if (!mounted) return;
    if (err == null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text('Đã cập nhật: $status'),
        backgroundColor: const Color(0xFFD85A30),
      ));
      _load();
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
        title: const Text('Đơn cần giao',
            style: TextStyle(fontWeight: FontWeight.w800)),
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : _orders.isEmpty
              ? _empty()
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

  Widget _empty() {
    return ListView(children: const [
      SizedBox(height: 120),
      Center(child: Text('🛵', style: TextStyle(fontSize: 64))),
      SizedBox(height: 12),
      Center(
        child: Text('Chưa có đơn cần giao',
            style: TextStyle(color: Colors.grey, fontWeight: FontWeight.w600)),
      ),
    ]);
  }

  Widget _buildCard(OrderModel order) {
    final shortId = order.id.length <= 6
        ? order.id.toUpperCase()
        : order.id.substring(order.id.length - 6).toUpperCase();
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
              Text('${order.totalPrice.toStringAsFixed(0)}đ',
                  style: const TextStyle(fontWeight: FontWeight.w800,
                      color: Color(0xFFD85A30))),
            ],
          ),
          const SizedBox(height: 6),
          Row(children: [
            const Icon(Icons.location_on, size: 15, color: Colors.grey),
            const SizedBox(width: 4),
            Expanded(child: Text(order.deliveryAddress,
                style: const TextStyle(fontSize: 12, color: Colors.grey))),
          ]),
          const Divider(height: 16),
          ...order.items.map((it) => Text('• ${it.productName} x${it.quantity}',
              style: const TextStyle(fontSize: 13))),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () => _setStatus(order, 'Delivering'),
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Color(0xFF2D7DD2)),
                  ),
                  child: const Text('Đang giao',
                      style: TextStyle(color: Color(0xFF2D7DD2),
                          fontWeight: FontWeight.w700)),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: ElevatedButton(
                  onPressed: () => _setStatus(order, 'Done'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF639922),
                  ),
                  child: const Text('Đã giao',
                      style: TextStyle(color: Colors.white,
                          fontWeight: FontWeight.w700)),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
