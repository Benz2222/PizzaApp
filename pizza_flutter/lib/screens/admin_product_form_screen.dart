import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../models/product.dart';
import '../models/category.dart';
import '../services/product_service.dart';
import '../services/category_service.dart';
import '../widgets/product_image.dart';

class AdminProductFormScreen extends StatefulWidget {
  final Product? product; // null = tạo mới
  const AdminProductFormScreen({super.key, this.product});

  @override
  State<AdminProductFormScreen> createState() => _AdminProductFormScreenState();
}

class _AdminProductFormScreenState extends State<AdminProductFormScreen> {
  final _nameCtrl = TextEditingController();
  final _descCtrl = TextEditingController();
  final _priceCtrl = TextEditingController();

  List<Category> _categories = [];
  String? _categoryId;
  String _imageUrl = '';
  bool _isAvailable = true;
  bool _uploading = false;
  bool _saving = false;
  String? _error;

  bool get _isEdit => widget.product != null;

  @override
  void initState() {
    super.initState();
    final p = widget.product;
    if (p != null) {
      _nameCtrl.text = p.name;
      _descCtrl.text = p.description;
      _priceCtrl.text = p.price.toStringAsFixed(0);
      _imageUrl = p.imageUrl;
      _categoryId = p.categoryId;
      _isAvailable = p.isAvailable;
    }
    _loadCategories();
  }

  Future<void> _loadCategories() async {
    final cats = await CategoryService.getAll();
    if (!mounted) return;
    setState(() {
      _categories = cats;
      _categoryId ??= cats.isNotEmpty ? cats.first.id : null;
    });
  }

  Future<void> _pickAndUpload() async {
    final picked = await ImagePicker().pickImage(source: ImageSource.gallery);
    if (picked == null) return;
    setState(() => _uploading = true);
    final url = await ProductService.uploadImage(picked);
    if (!mounted) return;
    setState(() {
      _uploading = false;
      if (url != null) {
        _imageUrl = url;
      } else {
        _error = 'Upload ảnh thất bại';
      }
    });
  }

  Future<void> _save() async {
    final price = double.tryParse(_priceCtrl.text.trim());
    if (_nameCtrl.text.trim().isEmpty || price == null || _categoryId == null) {
      setState(() => _error = 'Vui lòng nhập tên, giá và chọn danh mục');
      return;
    }
    setState(() { _saving = true; _error = null; });

    final err = _isEdit
        ? await ProductService.update(
            id: widget.product!.id,
            name: _nameCtrl.text.trim(),
            description: _descCtrl.text.trim(),
            price: price,
            imageUrl: _imageUrl,
            categoryId: _categoryId!,
            isAvailable: _isAvailable,
          )
        : await ProductService.create(
            name: _nameCtrl.text.trim(),
            description: _descCtrl.text.trim(),
            price: price,
            imageUrl: _imageUrl,
            categoryId: _categoryId!,
          );

    if (!mounted) return;
    setState(() => _saving = false);
    if (err == null) {
      Navigator.pop(context, true);
    } else {
      setState(() => _error = err);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: Text(_isEdit ? 'Sửa sản phẩm' : 'Thêm sản phẩm',
            style: const TextStyle(fontWeight: FontWeight.w800)),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Ảnh
          Center(
            child: Column(
              children: [
                Container(
                  width: 120, height: 120,
                  decoration: BoxDecoration(
                    color: const Color(0xFFFAECE7),
                    borderRadius: BorderRadius.circular(14),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: _uploading
                      ? const Center(child: CircularProgressIndicator(
                          color: Color(0xFFD85A30)))
                      : ProductImage(imageUrl: _imageUrl, emoji: '🍕', emojiSize: 48),
                ),
                TextButton.icon(
                  onPressed: _uploading ? null : _pickAndUpload,
                  icon: const Icon(Icons.upload, color: Color(0xFFD85A30)),
                  label: const Text('Chọn ảnh từ máy',
                      style: TextStyle(color: Color(0xFFD85A30))),
                ),
              ],
            ),
          ),
          _field('Tên sản phẩm', _nameCtrl),
          _field('Mô tả', _descCtrl, maxLines: 2),
          _field('Giá (đ)', _priceCtrl, keyboard: TextInputType.number),
          const SizedBox(height: 12),
          const Text('DANH MỤC',
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700,
                  color: Colors.grey, letterSpacing: 1)),
          const SizedBox(height: 6),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFD3D1C7)),
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<String>(
                isExpanded: true,
                value: _categoryId,
                items: _categories
                    .map((c) => DropdownMenuItem(value: c.id, child: Text(c.name)))
                    .toList(),
                onChanged: (v) => setState(() => _categoryId = v),
              ),
            ),
          ),
          if (_isEdit) ...[
            const SizedBox(height: 8),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              activeColor: const Color(0xFFD85A30),
              title: const Text('Đang bán'),
              value: _isAvailable,
              onChanged: (v) => setState(() => _isAvailable = v),
            ),
          ],
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: Colors.red)),
          ],
          const SizedBox(height: 20),
          SizedBox(
            height: 50,
            child: ElevatedButton(
              onPressed: _saving ? null : _save,
              style: ElevatedButton.styleFrom(
                backgroundColor: const Color(0xFFD85A30),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14)),
              ),
              child: _saving
                  ? const CircularProgressIndicator(color: Colors.white)
                  : Text(_isEdit ? 'LƯU' : 'THÊM',
                      style: const TextStyle(color: Colors.white,
                          fontWeight: FontWeight.w800, letterSpacing: 1)),
            ),
          ),
        ],
      ),
    );
  }

  Widget _field(String label, TextEditingController ctrl,
      {int maxLines = 1, TextInputType? keyboard}) {
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label.toUpperCase(),
              style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700,
                  color: Colors.grey, letterSpacing: 1)),
          const SizedBox(height: 6),
          TextField(
            controller: ctrl,
            maxLines: maxLines,
            keyboardType: keyboard,
            decoration: InputDecoration(
              filled: true,
              fillColor: Colors.white,
              contentPadding:
                  const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
              border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
              enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
              focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(10),
                  borderSide: const BorderSide(color: Color(0xFFD85A30))),
            ),
          ),
        ],
      ),
    );
  }
}
