import 'package:flutter/material.dart';
import '../core/constants.dart';

/// Hiển thị ảnh sản phẩm: nếu có ảnh thật thì load network, không thì fallback emoji.
class ProductImage extends StatelessWidget {
  final String imageUrl;
  final String emoji;
  final double emojiSize;
  final BoxFit fit;

  const ProductImage({
    super.key,
    required this.imageUrl,
    required this.emoji,
    this.emojiSize = 48,
    this.fit = BoxFit.cover,
  });

  @override
  Widget build(BuildContext context) {
    final url = AppConstants.fullImageUrl(imageUrl);
    if (url == null) return _emoji();
    return Image.network(
      url,
      fit: fit,
      width: double.infinity,
      height: double.infinity,
      errorBuilder: (_, __, ___) => _emoji(),
      loadingBuilder: (context, child, progress) =>
          progress == null ? child : _emoji(),
    );
  }

  Widget _emoji() => Center(
        child: Text(emoji, style: TextStyle(fontSize: emojiSize)),
      );
}
