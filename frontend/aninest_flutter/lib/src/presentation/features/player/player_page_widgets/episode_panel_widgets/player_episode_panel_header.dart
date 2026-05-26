import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerEpisodePanelHeader extends StatelessWidget {
  const PlayerEpisodePanelHeader({
    super.key,
    required this.title,
    required this.currentIndex,
    required this.totalCount,
  });

  final String title;
  final int? currentIndex;
  final int totalCount;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final counter = totalCount <= 0
        ? 'No episodes'
        : currentIndex == null
        ? '$totalCount episodes'
        : '${currentIndex! + 1} / $totalCount';

    return Container(
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 10),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: colorScheme.border)),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const Gap(3),
                Text(
                  counter,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    color: colorScheme.mutedForeground,
                  ),
                ),
              ],
            ),
          ),
          const Gap(10),
          Icon(
            BootstrapIcons.collectionPlay,
            size: 18,
            color: colorScheme.mutedForeground,
          ),
        ],
      ),
    );
  }
}
