import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryEmptyState extends StatelessWidget {
  const LibraryEmptyState({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 320),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(
              BootstrapIcons.collectionPlay,
              size: 32,
              color: colorScheme.mutedForeground,
            ),
            const Gap(12),
            const Text('No media folders yet').semiBold(),
            const Gap(6),
            Text(
              'Add a folder from the toolbar to start building your library.',
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
