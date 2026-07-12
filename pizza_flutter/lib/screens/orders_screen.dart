import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../models/order.dart';
import '../services/order_service.dart';

class OrdersScreen extends StatefulWidget {
  const OrdersScreen({super.key});

  @override
  State<OrdersScreen> createState() => _OrdersScreenState();
}

class _OrdersScreenState extends State<OrdersScreen> {
  List<OrderModel> _orders = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    final orders = await OrderService.getMyOrders();
    if (mounted) {
      setState(() {
        _orders = orders;
        _isLoading = false;
      });
    }
  }

  Future<void> _confirmCancel(OrderModel order) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Hủy đơn hàng?'),
        content: const Text('Bạn có chắc muốn hủy đơn hàng này?'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Không')),
          TextButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Hủy đơn',
                  style: TextStyle(color: Colors.red))),
        ],
      ),
    );
    if (ok != true) return;

    final error = await OrderService.cancelOrder(order.id);
    if (!mounted) return;
    if (error == null) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
        content: Text('Đã hủy đơn hàng'),
        backgroundColor: Color(0xFFD85A30),
      ));
      _load();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error), backgroundColor: Colors.red));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Đơn hàng của tôi',
            style: TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : _orders.isEmpty
              ? _buildEmpty()
              : RefreshIndicator(
                  onRefresh: _load,
                  color: const Color(0xFFD85A30),
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _orders.length,
                    itemBuilder: (_, i) => _buildOrderCard(_orders[i]),
                  ),
                ),
    );
  }

  Widget _buildEmpty() {
    return ListView(
      children: const [
        SizedBox(height: 120),
        Center(child: Text('📦', style: TextStyle(fontSize: 64))),
        SizedBox(height: 12),
        Center(
          child: Text('Chưa có đơn hàng nào',
              style: TextStyle(
                  fontSize: 16,
                  color: Colors.grey,
                  fontWeight: FontWeight.w600)),
        ),
      ],
    );
  }

  Widget _buildOrderCard(OrderModel order) {
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
                  style: const TextStyle(
                      fontWeight: FontWeight.w800, fontSize: 14)),
              _statusBadge(order),
            ],
          ),
          if (order.createdAt != null) ...[
            const SizedBox(height: 2),
            Text(_formatDate(order.createdAt!),
                style: const TextStyle(fontSize: 11, color: Colors.grey)),
          ],
          const Divider(height: 18),
          ...order.items.map((it) => Padding(
                padding: const EdgeInsets.only(bottom: 4),
                child: Row(
                  children: [
                    Expanded(
                      child: Text('${it.productName}  ·  Size ${it.size}',
                          style: const TextStyle(fontSize: 13),
                          maxLines: 1, overflow: TextOverflow.ellipsis),
                    ),
                    Text('x${it.quantity}',
                        style: const TextStyle(
                            fontSize: 13, color: Colors.grey)),
                  ],
                ),
              )),
          const SizedBox(height: 6),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  const Icon(Icons.location_on,
                      size: 14, color: Colors.grey),
                  const SizedBox(width: 4),
                  SizedBox(
                    width: 140,
                    child: Text(order.deliveryAddress,
                        style: const TextStyle(
                            fontSize: 11, color: Colors.grey),
                        maxLines: 1, overflow: TextOverflow.ellipsis),
                  ),
                ],
              ),
              Text('${order.totalPrice.toStringAsFixed(0)}đ',
                  style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      color: Color(0xFFD85A30),
                      fontSize: 15)),
            ],
          ),
          if (order.isUnpaid && order.paymentUrl.isNotEmpty) ...[
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 42,
              child: ElevatedButton.icon(
                onPressed: () => launchUrl(Uri.parse(order.paymentUrl),
                    mode: LaunchMode.externalApplication),
                icon: const Icon(Icons.payment, color: Colors.white, size: 18),
                label: const Text('THANH TOÁN NGAY',
                    style: TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 0.5)),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFD85A30),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ),
          ],
          if (order.status == 'AwaitingPayment') ...[
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              height: 40,
              child: OutlinedButton(
                onPressed: () => _confirmCancel(order),
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: Colors.red),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                ),
                child: const Text('HỦY ĐƠN',
                    style: TextStyle(color: Colors.red,
                        fontWeight: FontWeight.w700, letterSpacing: 0.5)),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _statusBadge(OrderModel order) {
    // Ưu tiên hiển thị trạng thái thanh toán khi chưa trả tiền
    late String label;
    late Color color;
    if (order.isUnpaid) {
      label = 'Chờ thanh toán';
      color = const Color(0xFFBA7517);
    } else {
      switch (order.status) {
        case 'Preparing':
          label = 'Đang chuẩn bị';
          color = const Color(0xFFD85A30);
          break;
        case 'Delivering':
          label = 'Đang giao';
          color = const Color(0xFF2D7DD2);
          break;
        case 'Done':
          label = 'Hoàn tất';
          color = const Color(0xFF639922);
          break;
        case 'Cancelled':
          label = 'Đã hủy';
          color = Colors.grey;
          break;
        default:
          label = order.status;
          color = const Color(0xFF639922);
      }
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(label,
          style: TextStyle(
              fontSize: 11, fontWeight: FontWeight.w700, color: color)),
    );
  }

  String _formatDate(DateTime d) {
    final local = d.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(local.day)}/${two(local.month)}/${local.year} ${two(local.hour)}:${two(local.minute)}';
  }
}
