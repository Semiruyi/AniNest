import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_episode_number_badge.dart';

class PlayerEpisodeListItem extends StatefulWidget {
  const PlayerEpisodeListItem({
    super.key,
    required this.item,
    required this.isSelected,
    required this.progressFraction,
    required this.onPressed,
  });

  final PlaylistItemDto item;
  final bool isSelected;
  final double progressFraction;
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
    final fileName = _fileName(widget.item.filePath);

    return MouseRegion(
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
          child: Padding(
            padding: const EdgeInsets.fromLTRB(10, 10, 10, 10),
            child: Row(
              children: <Widget>[
                PlayerEpisodeNumberBadge(
                  item: widget.item,
                  isSelected: widget.isSelected,
                ),
                const Gap(8),
                Expanded(
                  child: Column(
                    children: [
                      Expanded(
                        flex: 2,
                        child: Tooltip(
                          tooltip: (BuildContext context) =>
                              TooltipContainer(child: Text(fileName)),
                          child: Align(
                            alignment: Alignment.centerLeft,
                            child: Text(
                              fileName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: 13,
                                fontWeight: widget.isSelected
                                    ? FontWeight.w600
                                    : FontWeight.w500,
                                color: colorScheme.foreground,
                              ),
                            ),
                          ),
                        ),
                      ),
                      const Gap(8),
                      Expanded(
                        child: Center(
                          child: SizedBox(
                            width: double.infinity,
                            child: Progress(
                              progress: widget.progressFraction,
                              color: widget.isSelected
                                  ? colorScheme.primary
                                  : colorScheme.primary.withValues(alpha: 0.82),
                              backgroundColor: colorScheme.foreground
                                  .withValues(alpha: 0.08),
                            ),
                          ),
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

  String _fileName(String path) {
    final normalized = path.replaceAll('\\', '/');
    final segments = normalized.split('/');
    return segments.isEmpty ? path : segments.last;
  }
}
