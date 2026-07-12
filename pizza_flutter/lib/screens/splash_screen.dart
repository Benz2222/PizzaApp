import 'package:flutter/material.dart';
import '../services/auth_service.dart';
import 'home_screen.dart';
import 'login_screen.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    _decideStart();
  }

  Future<void> _decideStart() async {
    final token = await AuthService.getToken();

    Widget next;
    if (token == null || token.isEmpty) {
      next = const LoginScreen();
    } else {
      // Còn token -> kiểm tra còn hợp lệ không (token có thể hết hạn)
      final user = await AuthService.getMe();
      if (user != null) {
        next = const HomeScreen();
      } else {
        await AuthService.logout(); // token hỏng/hết hạn -> xóa, bắt đăng nhập lại
        next = const LoginScreen();
      }
    }

    if (!mounted) return;
    Navigator.pushReplacement(
        context, MaterialPageRoute(builder: (_) => next));
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
        child: const Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text('🍕', style: TextStyle(fontSize: 72)),
              SizedBox(height: 8),
              Text('Pizzolo',
                  style: TextStyle(fontSize: 32, fontWeight: FontWeight.w900,
                      color: Colors.white, letterSpacing: -1)),
              SizedBox(height: 24),
              CircularProgressIndicator(color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}
