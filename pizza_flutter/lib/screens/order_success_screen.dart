import 'package:flutter/material.dart';
import 'home_screen.dart';

class OrderSuccessScreen extends StatelessWidget {
  final int orderId;
  const OrderSuccessScreen({super.key, required this.orderId});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                width: 100, height: 100,
                decoration: const BoxDecoration(
                  color: Color(0xFFEAF3DE),
                  shape: BoxShape.circle,
                ),
                alignment: Alignment.center,
                child: const Text('✅',
                    style: TextStyle(fontSize: 48)),
              ),
              const SizedBox(height: 20),
              const Text('Đặt hàng thành công!',
                  style: TextStyle(fontSize: 24,
                      fontWeight: FontWeight.w900)),
              const SizedBox(height: 8),
              const Text(
                  'Đơn hàng đang được chuẩn bị.\nDự kiến giao trong 30–45 phút.',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontSize: 14, color: Colors.grey,
                      height: 1.6)),
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: 24, vertical: 10),
                decoration: BoxDecoration(
                  color: const Color(0xFFFAECE7),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Text('#PZL-$orderId',
                    style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: Color(0xFFD85A30),
                        fontSize: 16)),
              ),
              const SizedBox(height: 24),
              _buildTracker(),
              const SizedBox(height: 32),
              SizedBox(
                width: double.infinity,
                height: 50,
                child: ElevatedButton(
                  onPressed: () => Navigator.pushAndRemoveUntil(
                      context,
                      MaterialPageRoute(
                          builder: (_) => const HomeScreen()),
                          (route) => false),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFD85A30),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14)),
                  ),
                  child: const Text('VỀ TRANG CHỦ',
                      style: TextStyle(color: Colors.white,
                          fontWeight: FontWeight.w800, letterSpacing: 1)),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildTracker() {
    final steps = [
      {'label': 'Đã xác nhận', 'sub': 'Vừa xong', 'done': true},
      {'label': 'Đang làm bánh', 'sub': '~10 phút', 'active': true},
      {'label': 'Đang giao hàng', 'sub': '—', 'pending': true},
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
                width: 12, height: 12,
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
                          color: (done || active) ? Colors.black87 : Colors.grey))),
              Text(step['sub'] as String,
                  style: const TextStyle(fontSize: 11, color: Colors.grey)),
            ]),
          );
        }).toList(),
      ),
    );
  }
}