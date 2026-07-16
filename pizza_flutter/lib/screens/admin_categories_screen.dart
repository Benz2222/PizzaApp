import 'package:flutter/material.dart';
import '../models/category.dart';
import '../services/category_service.dart';

class AdminCategoriesScreen extends StatefulWidget {
  const AdminCategoriesScreen({super.key});

  @override
  State<AdminCategoriesScreen> createState() => _AdminCategoriesScreenState();
}

class _AdminCategoriesScreenState extends State<AdminCategoriesScreen> {
  List<Category> _categories = [];
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    final cats = await CategoryService.getAll();
    if (mounted) {
      setState(() {
        _categories = cats;
        _isLoading = false;
      });
    }
  }

  Future<void> _editDialog({Category? category}) async {
    final ctrl = TextEditingController(text: category?.name ?? '');
    final name = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(category == null ? 'Thêm danh mục' : 'Sửa danh mục'),
        content: TextField(
          controller: ctrl,
          autofocus: true,
          decoration: const InputDecoration(hintText: 'Tên danh mục'),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Hủy')),
          TextButton(
              onPressed: () => Navigator.pop(context, ctrl.text.trim()),
              child: const Text('Lưu')),
        ],
      ),
    );
    if (name == null || name.isEmpty) return;

    final err = category == null
        ? await CategoryService.create(name)
        : await CategoryService.update(category.id, name);
    if (!mounted) return;
    if (err == null) {
      _load();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(err), backgroundColor: Colors.red));
    }
  }

  Future<void> _delete(Category c) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Xóa danh mục?'),
        content: Text('Xóa "${c.name}"?'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Không')),
          TextButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Xóa', style: TextStyle(color: Colors.red))),
        ],
      ),
    );
    if (ok != true) return;
    final err = await CategoryService.delete(c.id);
    if (!mounted) return;
    if (err == null) {
      _load();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(err), backgroundColor: Colors.red));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Quản lý danh mục',
            style: TextStyle(fontWeight: FontWeight.w800)),
      ),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: const Color(0xFFD85A30),
        onPressed: () => _editDialog(),
        icon: const Icon(Icons.add, color: Colors.white),
        label: const Text('Thêm', style: TextStyle(color: Colors.white)),
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: _categories.length,
              itemBuilder: (_, i) {
                final c = _categories[i];
                return Container(
                  margin: const EdgeInsets.only(bottom: 10),
                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(c.name,
                            style: const TextStyle(fontWeight: FontWeight.w700)),
                      ),
                      IconButton(
                          onPressed: () => _editDialog(category: c),
                          icon: const Icon(Icons.edit,
                              color: Color(0xFFD85A30), size: 20)),
                      IconButton(
                          onPressed: () => _delete(c),
                          icon: const Icon(Icons.delete_outline,
                              color: Colors.red, size: 20)),
                    ],
                  ),
                );
              },
            ),
    );
  }
}
