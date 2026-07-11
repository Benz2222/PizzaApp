class OrderItemModel {
  final String productName;
  final String productImageUrl;
  final int quantity;
  final double unitPrice;
  final String size;

  OrderItemModel({
    required this.productName,
    required this.productImageUrl,
    required this.quantity,
    required this.unitPrice,
    required this.size,
  });

  factory OrderItemModel.fromJson(Map<String, dynamic> json) => OrderItemModel(
    productName: json['productName'] ?? '',
    productImageUrl: json['productImageUrl'] ?? '',
    quantity: json['quantity'] ?? 0,
    unitPrice: (json['unitPrice'] as num?)?.toDouble() ?? 0,
    size: json['size'] ?? 'M',
  );
}

class OrderModel {
  final String id;
  final double totalPrice;
  final String status;
  final String paymentStatus;
  final String paymentUrl;
  final String deliveryAddress;
  final DateTime? createdAt;
  final List<OrderItemModel> items;

  OrderModel({
    required this.id,
    required this.totalPrice,
    required this.status,
    required this.paymentStatus,
    required this.paymentUrl,
    required this.deliveryAddress,
    required this.createdAt,
    required this.items,
  });

  bool get isUnpaid => paymentStatus == 'Unpaid';

  factory OrderModel.fromJson(Map<String, dynamic> json) => OrderModel(
    id: json['id']?.toString() ?? '',
    totalPrice: (json['totalPrice'] as num?)?.toDouble() ?? 0,
    status: json['status'] ?? '',
    paymentStatus: json['paymentStatus'] ?? '',
    paymentUrl: json['paymentUrl'] ?? '',
    deliveryAddress: json['deliveryAddress'] ?? '',
    createdAt: DateTime.tryParse(json['createdAt'] ?? ''),
    items: (json['items'] as List? ?? [])
        .map((e) => OrderItemModel.fromJson(e))
        .toList(),
  );
}
