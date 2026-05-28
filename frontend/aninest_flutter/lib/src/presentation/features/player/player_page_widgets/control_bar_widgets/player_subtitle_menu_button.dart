import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_subtitle_track_menu.dart';
import 'player_transport_button.dart';

class PlayerSubtitleMenuButton extends StatefulWidget {
  const PlayerSubtitleMenuButton({super.key, required this.controller});

  final AppController controller;

  @override
  State<PlayerSubtitleMenuButton> createState() =>
      _PlayerSubtitleMenuButtonState();
}

class _PlayerSubtitleMenuButtonState extends State<PlayerSubtitleMenuButton> {
  final PopoverController _popoverController = PopoverController();

  @override
  void dispose() {
    _popoverController.dispose();
    super.dispose();
  }

  void _togglePopover() {
    if (_popoverController.hasOpenPopover) {
      _popoverController.close();
      return;
    }

    final runtime = widget.controller.playerRuntime;
    unawaited(
      _popoverController.show<void>(
        context: context,
        alignment: Alignment.bottomCenter,
        anchorAlignment: Alignment.topCenter,
        offset: const Offset(0, -8),
        modal: true,
        consumeOutsideTaps: false,
        dismissBackdropFocus: false,
        builder: (BuildContext context) {
          return PlayerSubtitleTrackMenu(
            tracks: runtime.subtitleTracks,
            selectedTrackId: runtime.selectedSubtitleTrackId,
            onTrackSelected: (String trackId) {
              unawaited(widget.controller.selectSubtitleTrack(trackId));
            },
            onDismissRequested: _popoverController.close,
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final runtime = widget.controller.playerRuntime;
    final isEnabled =
        widget.controller.canTogglePlayback && runtime.hasSelectableSubtitles;

    return PlayerTransportButton(
      tooltip: l10n.playerTooltipSubtitles,
      icon: BootstrapIcons.badgeCcFill,
      iconSize: 21,
      buttonSize: 30,
      enabled: isEnabled,
      onTap: isEnabled ? _togglePopover : null,
    );
  }
}
