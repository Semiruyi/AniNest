import 'dart:async';

import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'episode_panel_widgets/player_episode_panel_frame.dart';

class PlayerEpisodePanel extends StatefulWidget {
  const PlayerEpisodePanel({super.key, required this.controller});

  final PlayerController controller;

  @override
  State<PlayerEpisodePanel> createState() => _PlayerEpisodePanelState();
}

class _PlayerEpisodePanelState extends State<PlayerEpisodePanel> {
  final ScrollController _scrollController = ScrollController();
  String? _lastScrolledItemId;

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (BuildContext context, Widget? child) {
        final playlist = widget.controller.playlist;
        final selectedItemId = widget.controller.selectedItemId;
        _scrollToSelectedItem(playlist, selectedItemId);

        return PlayerEpisodePanelFrame(
          playlist: playlist,
          selectedItemId: selectedItemId,
          scrollController: _scrollController,
          onItemPressed: (String itemId) {
            if (itemId != selectedItemId) {
              unawaited(widget.controller.selectItem(itemId));
            }
          },
        );
      },
    );
  }

  void _scrollToSelectedItem(PlaylistDto? playlist, String? selectedItemId) {
    if (playlist == null || selectedItemId == null) {
      _lastScrolledItemId = null;
      return;
    }
    if (_lastScrolledItemId == selectedItemId) {
      return;
    }

    final index = playlist.items.indexWhere(
      (PlaylistItemDto item) => item.itemId == selectedItemId,
    );
    if (index < 0) {
      return;
    }

    _lastScrolledItemId = selectedItemId;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_scrollController.hasClients) {
        return;
      }

      final target = index * PlayerEpisodePanelFrame.itemExtent;
      final position = _scrollController.position;
      final clamped = target.clamp(0.0, position.maxScrollExtent);
      unawaited(
        _scrollController.animateTo(
          clamped,
          duration: const Duration(milliseconds: 220),
          curve: Curves.easeOutCubic,
        ),
      );
    });
  }
}
