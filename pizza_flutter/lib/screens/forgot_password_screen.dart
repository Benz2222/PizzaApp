import 'package:flutter/material.dart';
import '../services/auth_service.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _emailController = TextEditingController();
  final _tokenController = TextEditingController();
  final _passwordController = TextEditingController();

  bool _isLoading = false;
  bool _step2 = false; // false = nhập email, true = nhập token + mật khẩu mới
  String? _error;
  String? _info;

  Future<void> _requestToken() async {
    if (_emailController.text.trim().isEmpty) {
      setState(() => _error = 'Vui lòng nhập email');
      return;
    }
    setState(() { _isLoading = true; _error = null; _info = null; });
    final res = await AuthService.forgotPassword(_emailController.text.trim());
    if (!mounted) return;
    setState(() => _isLoading = false);
    if (res.error != null) {
      setState(() => _error = res.error);
      return;
    }
    setState(() {
      _step2 = true;
      // BE bản dev trả token thẳng trong response -> tự điền sẵn cho tiện test
      if (res.token != null) _tokenController.text = res.token!;
      _info = 'Đã tạo mã đặt lại. Nhập mật khẩu mới để hoàn tất.';
    });
  }

  Future<void> _resetPassword() async {
    if (_tokenController.text.trim().isEmpty ||
        _passwordController.text.trim().isEmpty) {
      setState(() => _error = 'Vui lòng nhập đủ mã và mật khẩu mới');
      return;
    }
    setState(() { _isLoading = true; _error = null; });
    final error = await AuthService.resetPassword(
      _emailController.text.trim(),
      _tokenController.text.trim(),
      _passwordController.text.trim(),
    );
    if (!mounted) return;
    setState(() => _isLoading = false);
    if (error != null) {
      setState(() => _error = error);
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
      content: Text('Đổi mật khẩu thành công! Hãy đăng nhập lại.'),
      backgroundColor: Color(0xFFD85A30),
    ));
    Navigator.pop(context);
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
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                Row(
                  children: [
                    IconButton(
                        onPressed: () => Navigator.pop(context),
                        icon: const Icon(Icons.arrow_back_ios,
                            color: Colors.white)),
                    const Text('Quên mật khẩu',
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
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(_step2
                          ? 'Nhập mã đặt lại và mật khẩu mới'
                          : 'Nhập email để nhận mã đặt lại mật khẩu',
                          style: const TextStyle(fontSize: 14,
                              color: Colors.grey)),
                      const SizedBox(height: 16),
                      _buildField('Email', _emailController,
                          Icons.email_outlined, enabled: !_step2),
                      if (_step2) ...[
                        const SizedBox(height: 12),
                        _buildField('Mã đặt lại', _tokenController,
                            Icons.vpn_key_outlined),
                        const SizedBox(height: 12),
                        _buildField('Mật khẩu mới', _passwordController,
                            Icons.lock_outlined, obscure: true),
                      ],
                      if (_info != null) ...[
                        const SizedBox(height: 8),
                        Text(_info!, style: const TextStyle(
                            color: Color(0xFF639922), fontSize: 12)),
                      ],
                      if (_error != null) ...[
                        const SizedBox(height: 8),
                        Text(_error!, style: const TextStyle(
                            color: Colors.red, fontSize: 13)),
                      ],
                      const SizedBox(height: 20),
                      SizedBox(
                        width: double.infinity,
                        height: 48,
                        child: ElevatedButton(
                          onPressed: _isLoading
                              ? null
                              : (_step2 ? _resetPassword : _requestToken),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFFD85A30),
                            shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(12)),
                          ),
                          child: _isLoading
                              ? const CircularProgressIndicator(
                                  color: Colors.white)
                              : Text(
                                  _step2 ? 'ĐẶT LẠI MẬT KHẨU' : 'GỬI YÊU CẦU',
                                  style: const TextStyle(color: Colors.white,
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: 1)),
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
    );
  }

  Widget _buildField(String label, TextEditingController ctrl, IconData icon,
      {bool obscure = false, bool enabled = true}) {
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
          enabled: enabled,
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
