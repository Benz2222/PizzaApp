import 'package:flutter/material.dart';
import '../models/order.dart';

const _unpaidColor = Color(0xFFBA7517);

/// Nhãn theo TÊN trạng thái (dùng cho dashboard - nơi chỉ có chuỗi, không có OrderModel).
String orderStatusLabelByName(String status) {
  switch (status) {
    case 'AwaitingPayment':
      return 'Chờ thanh toán';
    case 'Paid':
      return 'Đã thanh toán';
    case 'Preparing':
      return 'Đang chuẩn bị';
    case 'Ready':
      return 'Chờ giao';
    case 'Delivering':
      return 'Đang giao';
    case 'Done':
      return 'Đã giao';
    case 'Cancelled':
      return 'Đã hủy';
    default:
      return status;
  }
}

/// Màu theo TÊN trạng thái (dùng cho dashboard).
Color orderStatusColorByName(String status) {
  switch (status) {
    case 'AwaitingPayment':
      return _unpaidColor;
    case 'Paid':
      return const Color(0xFF8E44AD);
    case 'Preparing':
      return const Color(0xFFD85A30);
    case 'Ready':
      return const Color(0xFFBA7517);
    case 'Delivering':
      return const Color(0xFF2D7DD2);
    case 'Done':
      return const Color(0xFF639922);
    case 'Cancelled':
      return Colors.grey;
    default:
      return const Color(0xFF639922);
  }
}

String orderStatusLabel(OrderModel o) =>
    o.isUnpaid ? 'Chờ thanh toán' : orderStatusLabelByName(o.status);

Color orderStatusColor(OrderModel o) =>
    o.isUnpaid ? _unpaidColor : orderStatusColorByName(o.status);

Widget orderStatusBadge(OrderModel o) {
  final color = orderStatusColor(o);
  return Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(orderStatusLabel(o),
        style: TextStyle(
            fontSize: 11, fontWeight: FontWeight.w700, color: color)),
  );
}
