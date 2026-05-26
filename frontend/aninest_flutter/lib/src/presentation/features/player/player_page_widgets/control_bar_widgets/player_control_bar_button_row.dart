import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_transport_button.dart';

class PlayerControlBarButtonRow extends StatelessWidget {
  const PlayerControlBarButtonRow({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(6),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        child: Row(
          children: <Widget>[
          PlayerTransportButton(
            tooltip: l10n.playerTooltipPreviousEpisode,
            icon: BootstrapIcons.skipStartFill,
            iconSize: 25,
            buttonSize: 30,
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipPlay,
            icon: BootstrapIcons.playFill,
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipNextEpisode,
            icon: BootstrapIcons.skipEndFill,
            iconSize: 25,
            buttonSize: 30,
          ),
          const Spacer(),
          _PlayerUtilityButton(
            tooltip: l10n.playerTooltipPlaybackSpeed,
            label: '1.0x',
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipSubtitles,
            icon: BootstrapIcons.badgeCcFill,
            iconSize: 21,
            buttonSize: 30,
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipVolume,
            icon: BootstrapIcons.volumeUpFill,
            iconSize: 21,
            buttonSize: 30,
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipSettings,
            icon: BootstrapIcons.gearFill,
            iconSize: 19,
            buttonSize: 30,
          ),
          const SizedBox(width: 2),
          PlayerTransportButton(
            tooltip: l10n.playerTooltipFullscreen,
            icon: LucideIcons.fullscreen,
            iconSize: 23,
            buttonSize: 30,
          ),
        ],
      ),
    );
  }
}

class _PlayerUtilityButton extends StatefulWidget {
  const _PlayerUtilityButton({
    required this.tooltip,
    required this.label,
  });

  final String tooltip;
  final String label;

  @override
  State<_PlayerUtilityButton> createState() => _PlayerUtilityButtonState();
}

class _PlayerUtilityButtonState extends State<_PlayerUtilityButton> {
  bool _hovered = false;
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final backgroundColor = _pressed
        ? colorScheme.foreground.withValues(alpha: 0.18)
        : _hovered
            ? colorScheme.foreground.withValues(alpha: 0.12)
            : Colors.transparent;

    return Tooltip(
      tooltip: (context) => TooltipContainer(child: Text(widget.tooltip)),
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hovered = true),
        onExit: (_) => setState(() {
          _hovered = false;
          _pressed = false;
        }),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapDown: (_) => setState(() => _pressed = true),
          onTapCancel: () => setState(() => _pressed = false),
          onTapUp: (_) => setState(() => _pressed = false),
          onTap: () {},
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            curve: Curves.easeOut,
            height: 30,
            padding: const EdgeInsets.symmetric(horizontal: 10),
            decoration: BoxDecoration(
              color: backgroundColor,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Center(
              child: Text(
                widget.label,
                style: TextStyle(
                  color: colorScheme.foreground,
                  fontSize: 12,
                ),
              ).medium(),
            ),
          ),
        ),
      ),
    );
  }
}
