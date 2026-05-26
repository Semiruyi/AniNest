import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerEpisodeTextBlock extends StatelessWidget {
  const PlayerEpisodeTextBlock({
    super.key,
    required this.item,
    required this.isSelected,
  });

  final PlaylistItemDto item;
  final bool isSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final duration = _formatDuration(item.durationMs);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Text(
          item.title,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 13,
            fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
          ),
        ),
        const Gap(5),
        Text(
          duration ?? 'Episode ${item.index + 1}',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(fontSize: 12, color: colorScheme.mutedForeground),
        ),
      ],
    );
  }

  String? _formatDuration(int durationMs) {
    if (durationMs <= 0) {
      return null;
    }

    final duration = Duration(milliseconds: durationMs);
    final hours = duration.inHours;
    final minutes = duration.inMinutes.remainder(60);
    final seconds = duration.inSeconds.remainder(60);
    if (hours > 0) {
      return '$hours:${minutes.toString().padLeft(2, '0')}:'
          '${seconds.toString().padLeft(2, '0')}';
    }

    return '$minutes:${seconds.toString().padLeft(2, '0')}';
  }
}
