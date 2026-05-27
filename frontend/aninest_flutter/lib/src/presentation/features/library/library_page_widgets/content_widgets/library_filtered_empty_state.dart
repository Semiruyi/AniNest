import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryFilteredEmptyState extends StatelessWidget {
  const LibraryFilteredEmptyState({super.key, required this.view});

  final LibraryView view;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final copy = _copyFor(view);

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 320),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(copy.icon, size: 32, color: colorScheme.mutedForeground),
            const Gap(12),
            Text(copy.title).semiBold(),
            const Gap(6),
            Text(
              copy.message,
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

  _FilteredEmptyCopy _copyFor(LibraryView view) {
    return switch (view) {
      LibraryView.recentlyAdded => const _FilteredEmptyCopy(
        BootstrapIcons.clockHistory,
        'Nothing recently added',
        'New folders will appear here after they are added to the library.',
      ),
      LibraryView.continueWatching => const _FilteredEmptyCopy(
        BootstrapIcons.playCircle,
        'Nothing in progress',
        'Partially watched folders will appear here.',
      ),
      LibraryView.favorites => const _FilteredEmptyCopy(
        BootstrapIcons.heart,
        'No favorites yet',
        'Favorite a folder to keep it close at hand.',
      ),
      LibraryView.allMedia => const _FilteredEmptyCopy(
        BootstrapIcons.collectionPlay,
        'No media folders yet',
        'Add a folder from the toolbar to start building your library.',
      ),
    };
  }
}

class _FilteredEmptyCopy {
  const _FilteredEmptyCopy(this.icon, this.title, this.message);

  final IconData icon;
  final String title;
  final String message;
}
