class Product {
  final String id;
  final String name;
  final String description;
  final double price;
  final String imageUrl;
  final String categoryId;
  final String category; // = categoryName từ BE (giữ tên "category" cho các màn hình cũ)

  Product({
    required this.id,
    required this.name,
    required this.description,
    required this.price,
    required this.imageUrl,
    required this.categoryId,
    required this.category,
  });

  factory Product.fromJson(Map<String, dynamic> json) => Product(
    id: json['id']?.toString() ?? '',
    name: json['name'] ?? '',
    description: json['description'] ?? '',
    price: (json['price'] as num).toDouble(),
    imageUrl: json['imageUrl'] ?? '',
    categoryId: json['categoryId']?.toString() ?? '',
    category: json['categoryName'] ?? '',
  );
}
