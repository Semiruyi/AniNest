import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerEpisodePanelEmptyState extends StatelessWidget {
  const PlayerEpisodePanelEmptyState({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(
              BootstrapIcons.collectionPlay,
              size: 28,
              color: colorScheme.mutedForeground,
            ),
            const Gap(10),
            Text(
              'Open a folder to start watching',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 13,
                color: colorScheme.mutedForeground,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
