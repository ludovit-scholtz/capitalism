import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/auth/biatec_oidc_service.dart';
import '../../core/widgets/placeholder_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, this.oidcService = const BiatecOidcService()});

  final BiatecOidcService oidcService;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  bool _signingIn = false;

  Future<void> _signInWithBiatec() async {
    setState(() => _signingIn = true);
    final auth = context.read<AuthState>();
    final messenger = ScaffoldMessenger.of(context);

    try {
      final result = await widget.oidcService.signIn();
      await auth.setToken(result.token);
      messenger.showSnackBar(const SnackBar(content: Text('Signed in with Biatec.')));
    } on BiatecOidcException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    } finally {
      if (mounted) setState(() => _signingIn = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        const Expanded(child: PlaceholderScreen(title: 'Sign In', sourceView: 'LoginView.vue')),
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
          child: SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: _signingIn ? null : _signInWithBiatec,
              icon: _signingIn
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.login),
              label: const Text('Sign in with Biatec'),
            ),
          ),
        ),
      ],
    );
  }
}

class ForgotPasswordScreen extends StatelessWidget {
  const ForgotPasswordScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Forgot Password', sourceView: 'ForgotPasswordView.vue');
}

class ResetPasswordScreen extends StatelessWidget {
  const ResetPasswordScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Reset Password', sourceView: 'ResetPasswordView.vue');
}

class AuthCallbackScreen extends StatelessWidget {
  const AuthCallbackScreen({super.key});

  @override
  Widget build(BuildContext context) =>
      const PlaceholderScreen(title: 'Signing In…', sourceView: 'AuthCallbackView.vue');
}
