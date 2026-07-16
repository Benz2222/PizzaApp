import 'package:flutter/material.dart';
import '../services/auth_service.dart';
import 'home_screen.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _isLoading = false;
  String? _error;

  Future<void> _register() async {
    if (_nameController.text.isEmpty || _emailController.text.isEmpty ||
        _passwordController.text.isEmpty) {
      setState(() => _error = 'Vui lòng điền đầy đủ thông tin!');
      return;
    }
    setState(() { _isLoading = true; _error = null; });
    final error = await AuthService.register(
      _nameController.text.trim(),
      _emailController.text.trim(),
      _passwordController.text.trim(),
      _phoneController.text.trim(),
    );
    if (!mounted) return;
    setState(() => _isLoading = false);
    if (error == null) {
      Navigator.pushAndRemoveUntil(context,
          MaterialPageRoute(builder: (_) => const HomeScreen()),
              (route) => false);
    } else {
      setState(() => _error = error);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            colors: [Color(0xFFD85A30), Color(0xFF993C1D), Color(0xFF2C2C2A)],
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
          ),
        ),
        child: SafeArea(
          child: Center(child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                Row(
                  children: [
                    IconButton(
                        onPressed: () => Navigator.pop(context),
                        icon: const Icon(Icons.arrow_back_ios,
                            color: Colors.white)),
                    const Text('Tạo tài khoản',
                        style: TextStyle(fontSize: 20,
                            fontWeight: FontWeight.w800, color: Colors.white)),
                  ],
                ),
                const SizedBox(height: 16),
                Container(
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    children: [
                      _buildField('Họ tên', _nameController,
                          Icons.person_outlined),
                      const SizedBox(height: 12),
                      _buildField('Email', _emailController,
                          Icons.email_outlined),
                      const SizedBox(height: 12),
                      _buildField('Số điện thoại', _phoneController,
                          Icons.phone_outlined),
                      const SizedBox(height: 12),
                      _buildField('Mật khẩu', _passwordController,
                          Icons.lock_outlined, obscure: true),
                      if (_error != null) ...[
                        const SizedBox(height: 8),
                        Text(_error!,
                            style: const TextStyle(color: Colors.red,
                                fontSize: 13)),
                      ],
                      const SizedBox(height: 20),
                      SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: ElevatedButton(
                          onPressed: _isLoading ? null : _register,
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFFD85A30),
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(12)),
                          ),
                          child: _isLoading
                              ? const CircularProgressIndicator(
                              color: Colors.white)
                              : const Text('TẠO TÀI KHOẢN',
                              style: TextStyle(color: Colors.white,
                                  fontWeight: FontWeight.w800,
                                  letterSpacing: 1)),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          )),
        ),
      ),
    );
  }

  Widget _buildField(String label, TextEditingController ctrl,
      IconData icon, {bool obscure = false}) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label.toUpperCase(),
            style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700,
                color: Colors.grey, letterSpacing: 1)),
        const SizedBox(height: 6),
        TextField(
          controller: ctrl,
          obscureText: obscure,
          decoration: InputDecoration(
            prefixIcon: Icon(icon, color: const Color(0xFFD85A30), size: 20),
            border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
            enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: const BorderSide(color: Color(0xFFD3D1C7))),
            focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(10),
                borderSide: const BorderSide(color: Color(0xFFD85A30))),
            filled: true,
            fillColor: const Color(0xFFFDF8F3),
            contentPadding: const EdgeInsets.symmetric(
                horizontal: 12, vertical: 12),
          ),
        ),
      ],
    );
  }
}