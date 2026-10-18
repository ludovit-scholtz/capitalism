// Ported from `projects/frontend/src/components/layout/ChatSidePanel.vue`.

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:font_awesome_flutter/font_awesome_flutter.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../core/auth/auth_state.dart';
import '../../core/context/account_context_state.dart';
import '../../core/i18n/locale_state.dart';
import '../../core/theme/app_icons.dart';
import '../../l10n/generated/app_localizations.dart';
import 'chat_models.dart';
import 'chat_state.dart';

/// The chat itself: channel switch (global / selected city), message list, composer.
/// Shared by the bottom-sheet [ChatPanel] and the dashboard's Chat tab. Tells [ChatState] when it is visible so the
/// unread badge clears and polling speeds up.
class ChatView extends StatefulWidget {
  const ChatView({super.key});

  @override
  State<ChatView> createState() => _ChatViewState();
}

class _ChatViewState extends State<ChatView> {
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  late final ChatState _chat;
  String? _sendError;
  String? _lastNewestId;

  @override
  void initState() {
    super.initState();
    _chat = context.read<ChatState>();
    // Opening notifies listeners; that must not happen while this frame is still building.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) unawaited(_chat.openPanel());
    });
  }

  @override
  void dispose() {
    // Closing notifies listeners; never do that while the tree is being torn down.
    scheduleMicrotask(_chat.closePanel);
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final l10n = AppLocalizations.of(context);
    final outcome = await _chat.send(_controller.text);
    if (!mounted) return;
    setState(() {
      switch (outcome) {
        case ChatSendOutcome.sent:
          _controller.clear();
          _sendError = null;
        case ChatSendOutcome.empty:
          _sendError = null;
        case ChatSendOutcome.tooLong:
          _sendError = l10n.chatTooLong(maxChatLength);
        case ChatSendOutcome.rateLimited:
          _sendError = l10n.chatRateLimited;
        case ChatSendOutcome.failed:
          _sendError = l10n.chatSendFailed;
      }
    });
  }

  /// Follows the conversation when a new newest message arrives (keyed on its id: the history is capped, so the length stays constant),
  /// but leaves the reader alone when they scrolled up to read older messages.
  void _scrollToEndOnNewMessages(String? newestId) {
    if (newestId == _lastNewestId) return;
    final firstLoad = _lastNewestId == null;
    _lastNewestId = newestId;
    final wasNearBottom = !_scroll.hasClients || _scroll.position.maxScrollExtent - _scroll.offset < 120;
    if (!firstLoad && !wasNearBottom) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final chat = context.watch<ChatState>();
    final auth = context.watch<AuthState>();
    final cityContext = context.watch<AccountContextState>();
    final languageCode = context.watch<LocaleState>().languageCode;
    final cityId = cityContext.selectedCityId;
    final cityName = cityContext.selectedCityName;

    if (!auth.isAuthenticated) {
      return Center(child: Text(l10n.chatSignInRequired));
    }
    _scrollToEndOnNewMessages(chat.messages.isEmpty ? null : chat.messages.last.id);

    final timeFormat = DateFormat.yMd(languageCode).add_Hm();
    return Column(
      children: [
        if (cityId != null)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: SegmentedButton<bool>(
              segments: [
                ButtonSegment(value: false, label: Text(l10n.chatChannelGlobal)),
                ButtonSegment(value: true, label: Text(cityName ?? l10n.chatChannelCity)),
              ],
              selected: {chat.channelCityId != null},
              onSelectionChanged: (selection) => unawaited(chat.selectChannel(selection.first ? cityId : null)),
            ),
          ),
        Expanded(child: _buildMessages(context, chat, timeFormat)),
        if (_sendError != null)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(_sendError!, style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.error)),
          ),
        const SizedBox(height: 8),
        _buildComposer(context, chat, cityName),
      ],
    );
  }

  Widget _buildMessages(BuildContext context, ChatState chat, DateFormat timeFormat) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    if (chat.loading && chat.messages.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (chat.loadFailed && chat.messages.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l10n.chatLoadFailed),
            TextButton(onPressed: () => unawaited(chat.refresh()), child: Text(l10n.chatRetry)),
          ],
        ),
      );
    }
    if (chat.messages.isEmpty) {
      return Center(child: Text(l10n.chatEmpty));
    }
    return RefreshIndicator(
      onRefresh: chat.refresh,
      child: ListView.builder(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: chat.messages.length,
        itemBuilder: (context, index) {
          final message = chat.messages[index];
          final created = message.createdAt;
          final removed = message.isRemovedForViewer;
          return Align(
            alignment: message.isOwnMessage ? Alignment.centerRight : Alignment.centerLeft,
            child: Container(
              constraints: const BoxConstraints(maxWidth: 480),
              margin: const EdgeInsets.symmetric(vertical: 4),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: message.isOwnMessage ? theme.colorScheme.primaryContainer : theme.colorScheme.surfaceContainerHighest,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Semantics(
                label: '${message.authorDisplayName}: ${removed ? l10n.chatMessageRemoved : message.content}',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${message.authorDisplayName}${created == null ? '' : ' · ${timeFormat.format(created)}'}',
                      style: theme.textTheme.labelSmall,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      removed ? l10n.chatMessageRemoved : message.content,
                      style: removed ? theme.textTheme.bodyMedium?.copyWith(fontStyle: FontStyle.italic) : theme.textTheme.bodyMedium,
                    ),
                    if (message.isOwnMessage && !message.isVisible)
                      Text(l10n.chatHiddenFromOthers, style: theme.textTheme.labelSmall?.copyWith(color: theme.colorScheme.tertiary)),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildComposer(BuildContext context, ChatState chat, String? cityName) {
    final l10n = AppLocalizations.of(context);
    final length = _controller.text.length;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Expanded(
          child: TextField(
            controller: _controller,
            minLines: 1,
            maxLines: 4,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              hintText: chat.channelCityId != null
                  ? l10n.chatPlaceholderCity(cityName ?? l10n.chatChannelCity)
                  : l10n.chatPlaceholderGlobal,
              counterText: length >= chatCounterThreshold ? l10n.chatCharCounter(length, maxChatLength) : '',
              errorText: length > maxChatLength ? l10n.chatTooLong(maxChatLength) : null,
            ),
            onSubmitted: (_) => unawaited(_send()),
          ),
        ),
        const SizedBox(width: 8),
        IconButton.filled(
          tooltip: l10n.chatSend,
          onPressed: chat.sending || _controller.text.trim().isEmpty || length > maxChatLength ? null : () => unawaited(_send()),
          icon: chat.sending
              ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const FaIcon(AppIcons.send, size: 16),
        ),
      ],
    );
  }
}
