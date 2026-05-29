import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_anime4k_menu_button.dart';
import 'player_control_bar_state_controller.dart';
import 'player_subtitle_menu_button.dart';
import 'player_transport_button.dart';

class PlayerControlBarButtonRow extends StatelessWidget {
  const PlayerControlBarButtonRow({
    super.key,
    required this.controller,
    required this.isFullscreen,
    required this.onToggleFullscreen,
  });

  final PlayerControlBarStateController controller;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return AnimatedBuilder(
      animation: controller,
      builder: (BuildContext context, Widget? child) {
        final playIcon = controller.isPlaying
            ? BootstrapIcons.pauseFill
            : BootstrapIcons.playFill;
        final volumeIcon = controller.isMuted
            ? BootstrapIcons.volumeMuteFill
            : controller.playbackVolume < 50
            ? BootstrapIcons.volumeDownFill
            : BootstrapIcons.volumeUpFill;

        return Container(
          decoration: BoxDecoration(borderRadius: BorderRadius.circular(6)),
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
          child: Row(
            children: <Widget>[
              PlayerTransportButton(
                tooltip: l10n.playerTooltipPreviousEpisode,
                icon: BootstrapIcons.skipStartFill,
                iconSize: 25,
                buttonSize: 30,
                enabled: controller.canMovePrevious,
                onTap: controller.canMovePrevious
                    ? () => controller.appController.movePreviousAndPlay()
                    : null,
              ),
              const SizedBox(width: 2),
              PlayerTransportButton(
                tooltip: l10n.playerTooltipPlay,
                icon: playIcon,
                enabled: controller.canTogglePlayback,
                onTap: controller.canTogglePlayback
                    ? () => controller.appController.togglePlayPause()
                    : null,
              ),
              const SizedBox(width: 2),
              PlayerTransportButton(
                tooltip: l10n.playerTooltipNextEpisode,
                icon: BootstrapIcons.skipEndFill,
                iconSize: 25,
                buttonSize: 30,
                enabled: controller.canMoveNext,
                onTap: controller.canMoveNext
                    ? () => controller.appController.moveNextAndPlay()
                    : null,
              ),
              const Spacer(),
              _PlayerUtilityButton(
                tooltip: l10n.playerTooltipPlaybackSpeed,
                label: _formatRate(controller.playbackRate),
                enabled: controller.canTogglePlayback,
                onTap: controller.canTogglePlayback
                    ? () => controller.appController.cyclePlaybackRate()
                    : null,
              ),
              const SizedBox(width: 2),
              PlayerAnime4kMenuButton(controller: controller),
              const SizedBox(width: 2),
              PlayerSubtitleMenuButton(controller: controller),
              const SizedBox(width: 2),
              PlayerTransportButton(
                tooltip: l10n.playerTooltipVolume,
                icon: volumeIcon,
                iconSize: 21,
                buttonSize: 30,
                enabled: controller.canTogglePlayback,
                onTap: controller.canTogglePlayback
                    ? () => controller.appController.toggleMute()
                    : null,
              ),
              const SizedBox(width: 2),
              PlayerTransportButton(
                tooltip: l10n.playerTooltipSettings,
                icon: BootstrapIcons.gearFill,
                iconSize: 19,
                buttonSize: 30,
                enabled: false,
              ),
              const SizedBox(width: 2),
              PlayerTransportButton(
                tooltip: l10n.playerTooltipFullscreen,
                icon: isFullscreen
                    ? LucideIcons.minimize2
                    : LucideIcons.fullscreen,
                iconSize: 23,
                buttonSize: 30,
                enabled: true,
                onTap: onToggleFullscreen,
              ),
            ],
          ),
        );
      },
    );
  }

  String _formatRate(double rate) {
    final formatted = rate == rate.roundToDouble()
        ? rate.toStringAsFixed(0)
        : rate
              .toStringAsFixed(2)
              .replaceFirst(RegExp(r'0+$'), '')
              .replaceFirst(RegExp(r'\.$'), '');
    return '${formatted}x';
  }
}

class _PlayerUtilityButton extends StatefulWidget {
  const _PlayerUtilityButton({
    required this.tooltip,
    required this.label,
    required this.enabled,
    this.onTap,
  });

  final String tooltip;
  final String label;
  final bool enabled;
  final VoidCallback? onTap;

  @override
  State<_PlayerUtilityButton> createState() => _PlayerUtilityButtonState();
}

class _PlayerUtilityButtonState extends State<_PlayerUtilityButton> {
  bool _hovered = false;
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final isInteractive = widget.enabled && widget.onTap != null;
    final foregroundColor = isInteractive
        ? colorScheme.foreground
        : colorScheme.mutedForeground;

    return Tooltip(
      tooltip: (context) => TooltipContainer(child: Text(widget.tooltip)),
      child: MouseRegion(
        cursor: isInteractive
            ? SystemMouseCursors.click
            : SystemMouseCursors.basic,
        onEnter: isInteractive ? (_) => setState(() => _hovered = true) : null,
        onExit: isInteractive
            ? (_) => setState(() {
                _hovered = false;
                _pressed = false;
              })
            : null,
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapDown: isInteractive
              ? (_) => setState(() => _pressed = true)
              : null,
          onTapCancel: isInteractive
              ? () => setState(() => _pressed = false)
              : null,
          onTapUp: isInteractive
              ? (_) => setState(() => _pressed = false)
              : null,
          onTap: isInteractive ? widget.onTap : null,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            curve: Curves.easeOut,
            height: 30,
            padding: const EdgeInsets.symmetric(horizontal: 10),
            decoration: BoxDecoration(
              color: !isInteractive
                  ? Colors.transparent
                  : _pressed
                  ? colorScheme.foreground.withValues(alpha: 0.18)
                  : _hovered
                  ? colorScheme.foreground.withValues(alpha: 0.12)
                  : Colors.transparent,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Center(
              child: Text(
                widget.label,
                style: TextStyle(color: foregroundColor, fontSize: 12),
              ).medium(),
            ),
          ),
        ),
      ),
    );
  }
}
