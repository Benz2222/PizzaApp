import 'package:flutter/material.dart';
import '../models/product.dart';

class CartItem {
  final Product product;
  int quantity;
  String size;

  CartItem({required this.product, this.quantity = 1, this.size = 'M'});

  double get subtotal => product.price * quantity;
}

class CartProvider extends ChangeNotifier {
  final List<CartItem> _items = [];

  List<CartItem> get items => _items;

  int get totalCount => _items.fold(0, (sum, i) => sum + i.quantity);

  double get totalPrice => _items.fold(0, (sum, i) => sum + i.subtotal);

  void addItem(Product product, {String size = 'M'}) {
    final index = _items.indexWhere(
            (i) => i.product.id == product.id && i.size == size);
    if (index >= 0) {
      _items[index].quantity++;
    } else {
      _items.add(CartItem(product: product, size: size));
    }
    notifyListeners();
  }

  void removeItem(String productId, String size) {
    _items.removeWhere((i) => i.product.id == productId && i.size == size);
    notifyListeners();
  }

  void clear() {
    _items.clear();
    notifyListeners();
  }
}