import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerEpisodeNumberBadge extends StatelessWidget {
  const PlayerEpisodeNumberBadge({
    super.key,
    required this.item,
    required this.isSelected,
  });

  final PlaylistItemDto item;
  final bool isSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: 38,
      height: 38,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: isSelected
            ? colorScheme.primary.withValues(alpha: 0.16)
            : colorScheme.secondary,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        (item.index + 1).toString().padLeft(2, '0'),
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: isSelected ? colorScheme.primary : colorScheme.foreground,
        ),
      ),
    );
  }
}
