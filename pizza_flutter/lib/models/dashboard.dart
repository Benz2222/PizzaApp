class TopProduct {
  final String productName;
  final int quantity;
  final double revenue;
  TopProduct({required this.productName, required this.quantity, required this.revenue});

  factory TopProduct.fromJson(Map<String, dynamic> j) => TopProduct(
        productName: j['productName'] ?? '',
        quantity: (j['quantity'] as num?)?.toInt() ?? 0,
        revenue: (j['revenue'] as num?)?.toDouble() ?? 0,
      );
}

class OrderStats {
  final double revenueToday, revenueTotal;
  final int ordersToday, ordersTotal;
  final Map<String, int> byStatus;
  final List<TopProduct> topProducts;
  OrderStats({
    required this.revenueToday,
    required this.revenueTotal,
    required this.ordersToday,
    required this.ordersTotal,
    required this.byStatus,
    required this.topProducts,
  });

  factory OrderStats.fromJson(Map<String, dynamic> j) => OrderStats(
        revenueToday: (j['revenueToday'] as num?)?.toDouble() ?? 0,
        revenueTotal: (j['revenueTotal'] as num?)?.toDouble() ?? 0,
        ordersToday: (j['ordersToday'] as num?)?.toInt() ?? 0,
        ordersTotal: (j['ordersTotal'] as num?)?.toInt() ?? 0,
        byStatus: ((j['byStatus'] as Map?) ?? {})
            .map((k, v) => MapEntry(k.toString(), (v as num).toInt())),
        topProducts: ((j['topProducts'] as List?) ?? [])
            .map((e) => TopProduct.fromJson(e))
            .toList(),
      );
}

class AuthStats {
  final int totalUsers;
  final Map<String, int> byRole;
  AuthStats({required this.totalUsers, required this.byRole});

  factory AuthStats.fromJson(Map<String, dynamic> j) => AuthStats(
        totalUsers: (j['totalUsers'] as num?)?.toInt() ?? 0,
        byRole: ((j['byRole'] as Map?) ?? {})
            .map((k, v) => MapEntry(k.toString(), (v as num).toInt())),
      );
}

class ProductStats {
  final int totalProducts, available, unavailable;
  ProductStats({
    required this.totalProducts,
    required this.available,
    required this.unavailable,
  });

  factory ProductStats.fromJson(Map<String, dynamic> j) => ProductStats(
        totalProducts: (j['totalProducts'] as num?)?.toInt() ?? 0,
        available: (j['available'] as num?)?.toInt() ?? 0,
        unavailable: (j['unavailable'] as num?)?.toInt() ?? 0,
      );
}

class CategoryStats {
  final int totalCategories;
  CategoryStats({required this.totalCategories});

  factory CategoryStats.fromJson(Map<String, dynamic> j) => CategoryStats(
        totalCategories: (j['totalCategories'] as num?)?.toInt() ?? 0,
      );
}

/// Gộp 4 nguồn. Cái nào null = service đó lỗi -> UI hiện "—".
class DashboardData {
  final OrderStats? orders;
  final AuthStats? auth;
  final ProductStats? products;
  final CategoryStats? categories;
  DashboardData({this.orders, this.auth, this.products, this.categories});

  bool get allFailed =>
      orders == null && auth == null && products == null && categories == null;
}
