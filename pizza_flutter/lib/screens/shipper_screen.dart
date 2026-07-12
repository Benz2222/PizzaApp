import 'package:flutter/material.dart';
import '../models/order.dart';
import '../core/order_status.dart';
import '../services/order_service.dart';

class ShipperScreen extends StatefulWidget {
  const ShipperScreen({super.key});

  @override
  State<ShipperScreen> createState() => _ShipperScreenState();
}

class _ShipperScreenState extends State<ShipperScreen> {
  List<OrderModel> _available = [];
  List<OrderModel> _mine = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    final available = await OrderService.getAvailableOrders();
    final mine = await OrderService.getMyDeliveries();
    if (mounted) {
      setState(() {
        _available = available;
        _mine = mine;
        _isLoading = false;
      });
    }
  }

  Future<void> _act(Future<String?> Function() action, String okMsg) async {
    final err = await action();
    if (!mounted) return;
    if (err == null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(okMsg), backgroundColor: const Color(0xFFD85A30)));
      _load();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(err), backgroundColor: Colors.red));
    }
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        backgroundColor: const Color(0xFFFDF8F3),
        appBar: AppBar(
          backgroundColor: const Color(0xFFD85A30),
          foregroundColor: Colors.white,
          title: const Text('Giao hàng',
              style: TextStyle(fontWeight: FontWeight.w800)),
          bottom: TabBar(
            indicatorColor: Colors.white,
            labelStyle: const TextStyle(fontWeight: FontWeight.w800),
            tabs: [
              Tab(text: 'Chờ nhận (${_available.length})'),
              Tab(text: 'Đơn của tôi (${_mine.length})'),
            ],
          ),
        ),
        body: _isLoading
            ? const Center(
                child: CircularProgressIndicator(color: Color(0xFFD85A30)))
            : TabBarView(
                children: [
                  _buildList(_available, isAvailable: true),
                  _buildList(_mine, isAvailable: false),
                ],
              ),
      ),
    );
  }

  Widget _buildList(List<OrderModel> orders, {required bool isAvailable}) {
    if (orders.isEmpty) {
      return ListView(children: [
        const SizedBox(height: 120),
        Center(child: Text(isAvailable ? '🛵' : '📭',
            style: const TextStyle(fontSize: 64))),
        const SizedBox(height: 12),
        Center(
          child: Text(
              isAvailable ? 'Chưa có đơn chờ nhận' : 'Bạn chưa nhận đơn nào',
              style: const TextStyle(
                  color: Colors.grey, fontWeight: FontWeight.w600)),
        ),
      ]);
    }
    return RefreshIndicator(
      onRefresh: _load,
      color: const Color(0xFFD85A30),
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: orders.length,
        itemBuilder: (_, i) => _buildCard(orders[i], isAvailable),
      ),
    );
  }

  Widget _buildCard(OrderModel o, bool isAvailable) {
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
            const Icon(Icons.location_on, size: 15, color: Colors.grey),
            const SizedBox(width: 4),
            Expanded(child: Text(o.deliveryAddress,
                style: const TextStyle(fontSize: 12, color: Colors.grey))),
            Text('${o.totalPrice.toStringAsFixed(0)}đ',
                style: const TextStyle(fontWeight: FontWeight.w800,
                    color: Color(0xFFD85A30))),
          ]),
          const Divider(height: 16),
          ...o.items.map((it) => Text('• ${it.productName} x${it.quantity}',
              style: const TextStyle(fontSize: 13))),
          const SizedBox(height: 12),
          if (isAvailable)
            SizedBox(
              width: double.infinity,
              height: 42,
              child: ElevatedButton.icon(
                onPressed: () => _act(
                    () => OrderService.claimOrder(o.id), 'Đã nhận đơn'),
                icon: const Icon(Icons.motorcycle, color: Colors.white, size: 18),
                label: const Text('NHẬN ĐƠN',
                    style: TextStyle(color: Colors.white,
                        fontWeight: FontWeight.w800)),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFD85A30),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
              ),
            )
          else if (o.status == 'Delivering')
            Row(children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () => _act(
                      () => OrderService.setDeliveryStatus(o.id, 'Cancelled'),
                      'Đã hủy đơn'),
                  style: OutlinedButton.styleFrom(
                      side: const BorderSide(color: Colors.red)),
                  child: const Text('Hủy',
                      style: TextStyle(color: Colors.red,
                          fontWeight: FontWeight.w700)),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: ElevatedButton(
                  onPressed: () => _act(
                      () => OrderService.setDeliveryStatus(o.id, 'Done'),
                      'Đã giao thành công'),
                  style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF639922)),
                  child: const Text('Đã giao',
                      style: TextStyle(color: Colors.white,
                          fontWeight: FontWeight.w700)),
                ),
              ),
            ]),
        ],
      ),
    );
  }
}
