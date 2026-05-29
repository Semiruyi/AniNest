import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_episode_number_badge.dart';
import 'player_episode_status_icon.dart';
import 'player_episode_text_block.dart';

class PlayerEpisodeListItem extends StatefulWidget {
  const PlayerEpisodeListItem({
    super.key,
    required this.item,
    required this.isSelected,
    required this.onPressed,
  });

  final PlaylistItemDto item;
  final bool isSelected;
  final VoidCallback onPressed;

  @override
  State<PlayerEpisodeListItem> createState() => _PlayerEpisodeListItemState();
}

class _PlayerEpisodeListItemState extends State<PlayerEpisodeListItem> {
  bool _hovered = false;
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final progressFraction = _progressFraction(widget.item);

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
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
          onTap: widget.onPressed,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            curve: Curves.easeOut,
            decoration: BoxDecoration(
              color: widget.isSelected
                  ? colorScheme.primary.withValues(alpha: 0.12)
                  : _pressed
                  ? colorScheme.foreground.withValues(alpha: 0.09)
                  : _hovered
                  ? colorScheme.foreground.withValues(alpha: 0.06)
                  : Colors.transparent,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(
                color: widget.isSelected
                    ? colorScheme.primary.withValues(alpha: 0.38)
                    : colorScheme.border.withValues(alpha: 0.62),
              ),
            ),
            clipBehavior: Clip.antiAlias,
            child: Stack(
              children: <Widget>[
                if (widget.isSelected)
                  Positioned(
                    left: 0,
                    top: 0,
                    bottom: 0,
                    child: Container(width: 3, color: colorScheme.primary),
                  ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 8, 10, 8),
                  child: Row(
                    children: <Widget>[
                      PlayerEpisodeNumberBadge(
                        item: widget.item,
                        isSelected: widget.isSelected,
                      ),
                      const Gap(10),
                      Expanded(
                        child: PlayerEpisodeTextBlock(
                          item: widget.item,
                          isSelected: widget.isSelected,
                        ),
                      ),
                      const Gap(8),
                      PlayerEpisodeStatusIcon(
                        item: widget.item,
                        isSelected: widget.isSelected,
                      ),
                    ],
                  ),
                ),
                if (progressFraction > 0 && progressFraction < 1)
                  Positioned(
                    left: widget.isSelected ? 3 : 0,
                    right: 0,
                    bottom: 0,
                    child: FractionallySizedBox(
                      alignment: Alignment.centerLeft,
                      widthFactor: progressFraction,
                      child: Container(
                        height: 2,
                        color: colorScheme.primary.withValues(alpha: 0.72),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  double _progressFraction(PlaylistItemDto item) {
    if (!item.hasSavedProgress || item.durationMs <= 0) {
      return item.isPlayed ? 1 : 0;
    }

    return (item.savedProgressMs / item.durationMs).clamp(0.0, 1.0);
  }
}
