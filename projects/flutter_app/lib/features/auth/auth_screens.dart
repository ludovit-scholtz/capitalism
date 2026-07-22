// Ported from `projects/frontend/src/views/LoginView.vue`,
// `ForgotPasswordView.vue`, `ResetPasswordView.vue`, `AuthCallbackView.vue`,
// and the relevant parts of `stores/auth.ts`. Deliberately trimmed from the
// web version: no referral-code banner/auto-generated display name (the web
// store sends a `referralCode` field the server's `RegisterInput` doesn't
// actually have — looks like dead code there), no `oidc_retry=consent`
// drive-access-hint flow. Everything else — fields, validation, GraphQL
// mutation/REST endpoint names and args, error codes, and redirect targets
// — matches the web app; see ROADMAP.md history for the verification notes.

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/auth/biatec_oidc_service.dart';
import '../../core/auth/password_reset_service.dart';
import '../../core/config/app_config.dart';
import '../../core/graphql/graphql_service.dart';

const _loginMutation = r'''
  mutation Login($input: LoginInput!) {
    login(input: $input) { token expiresAtUtc }
  }
''';

const _registerMutation = r'''
  mutation Register($input: RegisterInput!) {
    register(input: $input) { token expiresAtUtc }
  }
''';

String? _validateEmail(String? value) {
  final trimmed = value?.trim() ?? '';
  if (trimmed.isEmpty) return 'Email is required.';
  if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(trimmed)) {
    return 'Please enter a valid email address.';
  }
  return null;
}

String? _validatePassword(String? value) {
  final v = value ?? '';
  if (v.isEmpty) return 'Password is required.';
  if (v.length < 8) return 'Password must be at least 8 characters.';
  return null;
}

class _Banner extends StatelessWidget {
  const _Banner(this.message, {required this.isError});

  final String message;
  final bool isError;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final background = isError ? scheme.errorContainer : Colors.green.withValues(alpha: 0.15);
    final foreground = isError ? scheme.onErrorContainer : Colors.green.shade800;
    return Semantics(
      liveRegion: true,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(12),
        margin: const EdgeInsets.only(bottom: 16),
        decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(8)),
        child: Text(message, style: TextStyle(color: foreground)),
      ),
    );
  }
}

/// Mirrors `LoginView.vue`. Field/mutation names and error-code mapping are
/// verified against `projects/frontend/src/stores/auth.ts` and
/// `projects/MasterApi/Types/Mutation.Auth.cs`; login/register are Master
/// API GraphQL mutations (not the game API `GraphQlService` normally talks
/// to), so calls here override `endpoint: AppConfig.masterGraphqlUrl`.
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, GraphQlService? graphQlService, this.passwordAuthEnabled, this.redirectPath = '/'})
    : _injectedGraphQlService = graphQlService;

  final GraphQlService? _injectedGraphQlService;

  /// Overrides [AppConfig.authPasswordEnabled] (which mirrors
  /// `VITE_AUTH_PASSWORD_ENABLED` — defaults to false, same as the web app,
  /// so by default this screen auto-redirects to Biatec sign-in rather than
  /// showing an email/password form) when set; see [effectivePasswordAuthEnabled].
  final bool? passwordAuthEnabled;

  bool get effectivePasswordAuthEnabled => passwordAuthEnabled ?? AppConfig.authPasswordEnabled;

  /// Where to land after a *password* login/register succeeds. The web app
  /// always uses `/` for this (see `handleSubmit` in `LoginView.vue`) — this
  /// only matters for the Biatec flow, which is redirect-target-aware.
  final String redirectPath;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _displayNameController = TextEditingController();

  late final GraphQlService _graphQlService;
  bool _isRegister = false;
  bool _submitting = false;
  bool _isThrottled = false;
  String? _formError;
  bool _autoRedirected = false;

  @override
  void initState() {
    super.initState();
    _graphQlService = widget._injectedGraphQlService ?? GraphQlService(context.read<AuthState>());
    if (!widget.effectivePasswordAuthEnabled) {
      // Mirrors LoginView.vue's onMounted: auto-fire Biatec sign-in after a
      // short delay when password auth is disabled server-side.
      Future.delayed(const Duration(milliseconds: 500), () {
        if (mounted && !_autoRedirected) _goToBiatecSignIn();
      });
    }
  }

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _displayNameController.dispose();
    super.dispose();
  }

  void _goToBiatecSignIn() {
    _autoRedirected = true;
    // The actual OIDC round trip (and its success/error handling) happens on
    // AuthCallbackScreen, mirroring the web: `startBiatecOidcSignIn` just
    // navigates the browser away, and AuthCallbackView is where the result
    // is shown once the IdP redirects back.
    context.go('/auth/callback?redirect=${Uri.encodeComponent(widget.redirectPath)}');
  }

  Future<void> _handleSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() {
      _submitting = true;
      _formError = null;
      _isThrottled = false;
    });

    final auth = context.read<AuthState>();
    try {
      final input = _isRegister
          ? {
              'email': _emailController.text.trim(),
              'displayName': _displayNameController.text.trim(),
              'password': _passwordController.text,
            }
          : {'email': _emailController.text.trim(), 'password': _passwordController.text};

      final data = await _graphQlService.request(
        _isRegister ? _registerMutation : _loginMutation,
        variables: {'input': input},
        endpoint: AppConfig.masterGraphqlUrl,
      );

      final payload = data[_isRegister ? 'register' : 'login'] as Map<String, dynamic>;
      await auth.setToken(payload['token'] as String);
      if (!mounted) return;
      context.go('/');
    } on GraphQlException catch (e) {
      if (!mounted) return;
      setState(() {
        switch (e.code) {
          case 'LOGIN_THROTTLED':
            _isThrottled = true;
          case 'INVALID_CREDENTIALS':
            _formError = 'Incorrect email or password.';
          case 'REGISTRATION_FAILED':
            _formError = 'Registration could not be completed. Please try a different email.';
          case 'AUTH_PASSWORD_DISABLED':
            _formError = 'Password sign-in is disabled on this server. Use Biatec sign-in instead.';
          case 'INVALID_EMAIL':
            _formError = 'Please enter a valid email address.';
          case 'DISPLAY_NAME_REQUIRED':
            _formError = 'Display name is required.';
          case 'PASSWORD_TOO_SHORT':
            _formError = 'Password must be at least 8 characters.';
          default:
            _formError = e.message;
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _formError = 'An error occurred.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!widget.effectivePasswordAuthEnabled) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircularProgressIndicator(),
              SizedBox(height: 16),
              Text('This server uses Biatec sign-in only.'),
              SizedBox(height: 4),
              Text('Redirecting to sign-in…'),
            ],
          ),
        ),
      );
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(_isRegister ? 'Create Account' : 'Sign In', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 16),
            if (_isThrottled)
              const _Banner('Too many sign-in attempts. Please wait a moment before trying again.', isError: true)
            else if (_formError != null)
              _Banner(_formError!, isError: true),
            if (_isRegister) ...[
              TextFormField(
                key: const Key('login-display-name'),
                controller: _displayNameController,
                decoration: const InputDecoration(labelText: 'Display Name'),
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Display name is required.' : null,
              ),
              const SizedBox(height: 12),
            ],
            TextFormField(
              key: const Key('login-email'),
              controller: _emailController,
              keyboardType: TextInputType.emailAddress,
              decoration: const InputDecoration(labelText: 'Email'),
              validator: _validateEmail,
            ),
            const SizedBox(height: 12),
            TextFormField(
              key: const Key('login-password'),
              controller: _passwordController,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Password'),
              validator: _validatePassword,
            ),
            if (!_isRegister)
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  onPressed: () => context.go('/forgot-password'),
                  child: const Text('Forgot password?'),
                ),
              ),
            const SizedBox(height: 8),
            FilledButton(
              onPressed: _submitting ? null : _handleSubmit,
              child: _submitting
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : Text(_isRegister ? 'Create Account' : 'Sign In'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _goToBiatecSignIn,
              icon: const Icon(Icons.login),
              label: const Text('Sign in with Biatec'),
            ),
            const SizedBox(height: 16),
            TextButton(
              onPressed: () => setState(() => _isRegister = !_isRegister),
              child: Text(_isRegister ? 'Already have an account? Sign in' : "Don't have an account? Create one"),
            ),
          ],
        ),
      ),
    );
  }
}

/// Mirrors `ForgotPasswordView.vue`. Not a GraphQL mutation — a REST POST
/// to the Master API (see `PasswordResetService`), matching the web's
/// `requestPasswordReset()` in `lib/passwordReset.ts`.
class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key, PasswordResetService? passwordResetService})
    : _injectedService = passwordResetService;

  final PasswordResetService? _injectedService;

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  late final PasswordResetService _service;
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  bool _submitting = false;
  String? _errorMessage;
  String? _successMessage;

  @override
  void initState() {
    super.initState();
    _service = widget._injectedService ?? PasswordResetService();
  }

  @override
  void dispose() {
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _handleSubmit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() {
      _submitting = true;
      _errorMessage = null;
      _successMessage = null;
    });

    try {
      final message = await _service.requestReset(_emailController.text.trim());
      if (!mounted) return;
      setState(() => _successMessage = message);
    } on PasswordResetException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.code == 'METHOD_NOT_ALLOWED'
            ? 'Password sign-in is disabled on this server. Use Biatec sign-in instead.'
            : e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Forgot Password', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 8),
            const Text('Enter your email and we will send you a reset link.'),
            const SizedBox(height: 16),
            if (_errorMessage != null) _Banner(_errorMessage!, isError: true),
            if (_successMessage != null) _Banner(_successMessage!, isError: false),
            TextFormField(
              key: const Key('forgot-password-email'),
              controller: _emailController,
              keyboardType: TextInputType.emailAddress,
              decoration: const InputDecoration(labelText: 'Email'),
              validator: _validateEmail,
              enabled: _successMessage == null,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: (_submitting || _successMessage != null) ? null : _handleSubmit,
              child: _submitting
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Send Reset Link'),
            ),
            const SizedBox(height: 8),
            TextButton(onPressed: () => context.go('/login'), child: const Text('Back to Sign In')),
          ],
        ),
      ),
    );
  }
}

/// Mirrors `ResetPasswordView.vue`. [token] comes from the `?token=...`
/// query param on the `/reset-password` route (see `app_router.dart`), not
/// a path segment — matching the web's `route.query.token`. Not a GraphQL
/// mutation — a REST POST to the Master API, matching `resetPassword()` in
/// `lib/passwordReset.ts`. The server combines "invalid" and "already used"
/// and "expired" into one `RESET_TOKEN_INVALID_OR_EXPIRED` code, so there is
/// no more specific message to show than the web shows.
class ResetPasswordScreen extends StatefulWidget {
  const ResetPasswordScreen({super.key, this.token, PasswordResetService? passwordResetService})
    : _injectedService = passwordResetService;

  final String? token;
  final PasswordResetService? _injectedService;

  @override
  State<ResetPasswordScreen> createState() => _ResetPasswordScreenState();
}

class _ResetPasswordScreenState extends State<ResetPasswordScreen> {
  late final PasswordResetService _service;
  final _formKey = GlobalKey<FormState>();
  final _newPasswordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _submitting = false;
  String? _errorMessage;
  String? _successMessage;

  @override
  void initState() {
    super.initState();
    _service = widget._injectedService ?? PasswordResetService();
    _newPasswordController.addListener(_onPasswordChanged);
  }

  void _onPasswordChanged() => setState(() {});

  @override
  void dispose() {
    _newPasswordController.removeListener(_onPasswordChanged);
    _newPasswordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  String get _passwordStrengthLabel {
    final length = _newPasswordController.text.length;
    if (length >= 12) return 'Strong';
    if (length >= 8) return 'Medium';
    return 'Weak';
  }

  Future<void> _handleSubmit() async {
    final token = widget.token;
    if (token == null || token.isEmpty) {
      setState(() => _errorMessage = 'This reset link is missing its token. Please request a new one.');
      return;
    }
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_newPasswordController.text != _confirmPasswordController.text) {
      setState(() => _errorMessage = 'Passwords do not match.');
      return;
    }

    setState(() {
      _submitting = true;
      _errorMessage = null;
    });

    try {
      final message = await _service.resetPassword(token: token, newPassword: _newPasswordController.text);
      if (!mounted) return;
      setState(() => _successMessage = message);
      Future.delayed(const Duration(seconds: 2), () {
        if (mounted) context.go('/login');
      });
    } on PasswordResetException catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.code == 'METHOD_NOT_ALLOWED'
            ? 'Password sign-in is disabled on this server. Use Biatec sign-in instead.'
            : e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _errorMessage = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Reset Password', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 16),
            if (_errorMessage != null) _Banner(_errorMessage!, isError: true),
            if (_successMessage != null) _Banner(_successMessage!, isError: false),
            TextFormField(
              key: const Key('reset-new-password'),
              controller: _newPasswordController,
              obscureText: true,
              autocorrect: false,
              decoration: const InputDecoration(labelText: 'New Password'),
              validator: _validatePassword,
              enabled: _successMessage == null,
            ),
            if (_newPasswordController.text.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 4, bottom: 8),
                child: Text(
                  'Password strength: $_passwordStrengthLabel',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              )
            else
              const SizedBox(height: 12),
            TextFormField(
              key: const Key('reset-confirm-password'),
              controller: _confirmPasswordController,
              obscureText: true,
              autocorrect: false,
              decoration: const InputDecoration(labelText: 'Confirm Password'),
              validator: _validatePassword,
              enabled: _successMessage == null,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: (_submitting || _successMessage != null) ? null : _handleSubmit,
              child: _submitting
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Reset Password'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Mirrors `AuthCallbackView.vue`, adapted for a client-driven OIDC flow: on
/// native platforms there is no server redirect round trip to resume after —
/// `flutter_web_auth_2` returns the callback URL directly to the caller — so
/// this screen owns the actual [BiatecOidcService.signIn] call rather than
/// just parsing an already-completed redirect out of the current URL like
/// the web does. [providerError]/[providerErrorDescription] cover the case
/// where this route is reached with an error already attached (the direct
/// analogue of the web's `detectOidcProviderError()`).
class AuthCallbackScreen extends StatefulWidget {
  const AuthCallbackScreen({
    super.key,
    this.oidcService = const BiatecOidcService(),
    this.providerError,
    this.providerErrorDescription,
    this.redirectPath = '/',
  });

  final BiatecOidcService oidcService;
  final String? providerError;
  final String? providerErrorDescription;
  final String redirectPath;

  @override
  State<AuthCallbackScreen> createState() => _AuthCallbackScreenState();
}

class _AuthCallbackScreenState extends State<AuthCallbackScreen> {
  String? _error;

  @override
  void initState() {
    super.initState();
    if (widget.providerError != null) {
      _error = widget.providerErrorDescription ?? 'Sign-in failed: ${widget.providerError}';
      return;
    }
    _completeSignIn();
  }

  Future<void> _completeSignIn() async {
    final auth = context.read<AuthState>();
    try {
      final result = await widget.oidcService.signIn();
      await auth.setToken(result.token);
      if (!mounted) return;
      context.go(widget.redirectPath);
    } on BiatecOidcException catch (e) {
      if (!mounted) return;
      setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final error = _error;

    if (error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.error_outline, color: theme.colorScheme.error, size: 40),
              const SizedBox(height: 12),
              Text(error, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(onPressed: () => context.go('/login'), child: const Text('Sign In')),
            ],
          ),
        ),
      );
    }

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(),
            const SizedBox(height: 16),
            Text('Completing sign-in…', style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            const Text('Please wait while we finish signing you in.'),
          ],
        ),
      ),
    );
  }
}
