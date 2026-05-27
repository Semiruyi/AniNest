import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_top_bar_badge.dart';
import 'player_top_bar_playback_info.dart';

class PlayerTopBarFrame extends StatelessWidget {
  const PlayerTopBarFrame({super.key, required this.info});

  final PlayerTopBarPlaybackInfo info;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      decoration: BoxDecoration(
        color: colorScheme.background,
        border: Border(bottom: BorderSide(color: colorScheme.border)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: LayoutBuilder(
        builder: (BuildContext context, BoxConstraints constraints) {
          final isCompact = constraints.maxWidth < 460;

          return Row(
            children: <Widget>[
              if (isCompact)
                PlayerTopBarBadge(
                  label: '${info.episodeLabel} | ${info.videoFormat}',
                )
              else ...<Widget>[
                PlayerTopBarBadge(label: info.episodeLabel),
                const Gap(8),
                PlayerTopBarBadge(label: info.videoFormat),
              ],
              const Gap(10),
              Expanded(
                child: Tooltip(
                  tooltip: (BuildContext context) => TooltipContainer(
                    child: Text(info.filePath ?? 'No media selected'),
                  ),
                  child: Text(
                    info.fileName ?? 'No media selected',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: info.hasSelectedMedia
                          ? colorScheme.foreground
                          : colorScheme.mutedForeground,
                    ),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
