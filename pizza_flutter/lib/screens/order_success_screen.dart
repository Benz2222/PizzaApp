import 'dart:async';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../services/order_service.dart';
import 'home_screen.dart';
import 'orders_screen.dart';

class OrderSuccessScreen extends StatefulWidget {
  final String orderId;
  final String paymentUrl;
  const OrderSuccessScreen({
    super.key,
    required this.orderId,
    this.paymentUrl = '',
  });

  @override
  State<OrderSuccessScreen> createState() => _OrderSuccessScreenState();
}

class _OrderSuccessScreenState extends State<OrderSuccessScreen>
    with WidgetsBindingObserver {
  Timer? _timer;
  bool _paid = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    // Poll trạng thái đơn: thanh toán xong (qua event) là màn hình tự đổi.
    _timer = Timer.periodic(const Duration(seconds: 3), (_) => _check());
    _check();
  }

  @override
  void dispose() {
    _timer?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  // Quay lại app từ trình duyệt thanh toán -> kiểm tra ngay, không đợi 3s.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _check();
  }

  Future<void> _check() async {
    if (_paid || !mounted) return;
    try {
      final orders = await OrderService.getMyOrders();
      final o = orders.where((e) => e.id == widget.orderId);
      if (o.isNotEmpty && o.first.paymentStatus == 'Paid' && mounted) {
        setState(() => _paid = true);
        _timer?.cancel();
      }
    } catch (_) {
      // lỗi mạng tạm thời -> lần poll sau thử lại
    }
  }

  String get _shortId {
    final id = widget.orderId;
    if (id.length <= 6) return id.toUpperCase();
    return id.substring(id.length - 6).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const SizedBox(height: 20),
              Container(
                width: 100,
                height: 100,
                decoration: BoxDecoration(
                  color: _paid
                      ? const Color(0xFFD7F0D2)
                      : const Color(0xFFEAF3DE),
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: Text(_paid ? '🎉' : '✅',
                    style: const TextStyle(fontSize: 48)),
              ),
              const SizedBox(height: 20),
              Text(_paid ? 'Đã thanh toán!' : 'Đặt hàng thành công!',
                  style: const TextStyle(
                      fontSize: 24, fontWeight: FontWeight.w900)),
              const SizedBox(height: 8),
              Text(
                  _paid
                      ? 'Cảm ơn bạn! Đơn đang được chuẩn bị.\nDự kiến giao trong 30–45 phút.'
                      : 'Vui lòng hoàn tất thanh toán để đơn được xử lý.\nDự kiến giao trong 30–45 phút.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                      fontSize: 14, color: Colors.grey, height: 1.6)),
              const SizedBox(height: 20),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 24, vertical: 10),
                decoration: BoxDecoration(
                  color: const Color(0xFFFAECE7),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Text('#PZL-$_shortId',
                    style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: Color(0xFFD85A30),
                        fontSize: 16)),
              ),
              const SizedBox(height: 24),
              _buildTracker(),
              const SizedBox(height: 24),
              // Chưa trả tiền -> nút mở lại link + báo đang chờ
              if (!_paid && widget.paymentUrl.isNotEmpty) ...[
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton.icon(
                    onPressed: () => launchUrl(Uri.parse(widget.paymentUrl),
                        mode: LaunchMode.externalApplication),
                    icon: const Icon(Icons.qr_code, color: Colors.white),
                    label: const Text('MỞ LẠI LINK THANH TOÁN',
                        style: TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 1)),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFD85A30),
                      shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(14)),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    SizedBox(
                        width: 14,
                        height: 14,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Color(0xFFD85A30))),
                    SizedBox(width: 10),
                    Text('Đang chờ thanh toán…',
                        style: TextStyle(
                            fontSize: 13,
                            color: Color(0xFFD85A30),
                            fontWeight: FontWeight.w600)),
                  ],
                ),
              ],
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                height: 50,
                child: OutlinedButton(
                  onPressed: () => Navigator.pushAndRemoveUntil(
                      context,
                      MaterialPageRoute(builder: (_) => const OrdersScreen()),
                      (route) => route.isFirst),
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Color(0xFFD85A30)),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14)),
                  ),
                  child: const Text('XEM ĐƠN HÀNG',
                      style: TextStyle(
                          color: Color(0xFFD85A30),
                          fontWeight: FontWeight.w800,
                          letterSpacing: 1)),
                ),
              ),
              const SizedBox(height: 10),
              TextButton(
                onPressed: () => Navigator.pushAndRemoveUntil(
                    context,
                    MaterialPageRoute(builder: (_) => const HomeScreen()),
                    (route) => false),
                child: const Text('Về trang chủ',
                    style: TextStyle(
                        color: Colors.grey, fontWeight: FontWeight.w700)),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTracker() {
    final steps = [
      {'label': 'Đã đặt hàng', 'sub': 'Vừa xong', 'done': true},
      {
        'label': _paid ? 'Đã thanh toán' : 'Chờ thanh toán',
        'sub': _paid ? 'Xong' : 'Quét QR',
        if (_paid) 'done': true else 'active': true,
      },
      {
        'label': 'Đang làm bánh',
        'sub': _paid ? 'Sắp tới' : '—',
        if (_paid) 'active': true else 'pending': true,
      },
      {'label': 'Đã giao', 'sub': '—', 'pending': true},
    ];
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
      ),
      child: Column(
        children: steps.map((step) {
          final done = step['done'] == true;
          final active = step['active'] == true;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(children: [
              Container(
                width: 12,
                height: 12,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: done
                      ? const Color(0xFF639922)
                      : active
                          ? const Color(0xFFD85A30)
                          : const Color(0xFFD3D1C7),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                  child: Text(step['label'] as String,
                      style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                          color: (done || active)
                              ? Colors.black87
                              : Colors.grey))),
              Text(step['sub'] as String,
                  style: const TextStyle(fontSize: 11, color: Colors.grey)),
            ]),
          );
        }).toList(),
      ),
    );
  }
}
