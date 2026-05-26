import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerEpisodeStatusIcon extends StatelessWidget {
  const PlayerEpisodeStatusIcon({
    super.key,
    required this.item,
    required this.isSelected,
  });

  final PlaylistItemDto item;
  final bool isSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    if (isSelected) {
      return Icon(
        BootstrapIcons.playFill,
        size: 16,
        color: colorScheme.primary,
      );
    }
    if (item.isPlayed) {
      return Icon(
        BootstrapIcons.checkCircleFill,
        size: 15,
        color: colorScheme.mutedForeground,
      );
    }
    if (item.hasSavedProgress) {
      return Icon(
        BootstrapIcons.clockHistory,
        size: 15,
        color: colorScheme.mutedForeground,
      );
    }

    return const SizedBox(width: 16, height: 16);
  }
}
