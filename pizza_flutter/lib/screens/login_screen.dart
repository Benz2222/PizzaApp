import 'package:flutter/material.dart';
import '../services/auth_service.dart';
import 'home_screen.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _isLoading = false;
  String? _error;

  Future<void> _login() async {
    setState(() { _isLoading = true; _error = null; });
    final success = await AuthService.login(
      _emailController.text.trim(),
      _passwordController.text.trim(),
    );
    setState(() => _isLoading = false);
    if (success && mounted) {
      Navigator.pushReplacement(
          context, MaterialPageRoute(builder: (_) => const HomeScreen()));
    } else {
      setState(() => _error = 'Email hoặc mật khẩu không đúng!');
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
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  const Text('🍕', style: TextStyle(fontSize: 64)),
                  const Text('Pizzolo',
                      style: TextStyle(fontSize: 36, fontWeight: FontWeight.w900,
                          color: Colors.white, letterSpacing: -1)),
                  const SizedBox(height: 4),
                  const Text('FRESH. HOT. DELIVERED.',
                      style: TextStyle(fontSize: 11, color: Colors.white60,
                          letterSpacing: 2)),
                  const SizedBox(height: 32),
                  Container(
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(20),
                    ),
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Đăng nhập',
                            style: TextStyle(fontSize: 20,
                                fontWeight: FontWeight.w800)),
                        const SizedBox(height: 20),
                        _buildField('Email', _emailController,
                            Icons.email_outlined),
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
                            onPressed: _isLoading ? null : _login,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: const Color(0xFFD85A30),
                              shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(12)),
                            ),
                            child: _isLoading
                                ? const CircularProgressIndicator(color: Colors.white)
                                : const Text('ĐĂNG NHẬP',
                                style: TextStyle(color: Colors.white,
                                    fontWeight: FontWeight.w800,
                                    letterSpacing: 1)),
                          ),
                        ),
                        const SizedBox(height: 16),
                        Center(
                          child: GestureDetector(
                            onTap: () => Navigator.push(context,
                                MaterialPageRoute(
                                    builder: (_) => const RegisterScreen())),
                            child: const Text.rich(TextSpan(children: [
                              TextSpan(text: 'Chưa có tài khoản? ',
                                  style: TextStyle(color: Colors.grey)),
                              TextSpan(text: 'Đăng ký ngay',
                                  style: TextStyle(color: Color(0xFFD85A30),
                                      fontWeight: FontWeight.w700)),
                            ])),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
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
