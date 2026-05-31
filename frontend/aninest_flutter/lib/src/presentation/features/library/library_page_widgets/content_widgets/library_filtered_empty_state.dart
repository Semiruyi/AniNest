import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryFilteredEmptyState extends StatelessWidget {
  const LibraryFilteredEmptyState({
    super.key,
    required this.view,
    required this.searchQuery,
    required this.hasViewResults,
  });

  final LibraryView view;
  final String searchQuery;
  final bool hasViewResults;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    final copy = _copyFor(view, l10n);

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

  _FilteredEmptyCopy _copyFor(LibraryView view, AppLocalizations l10n) {
    if (hasViewResults && searchQuery.trim().isNotEmpty) {
      return _FilteredEmptyCopy(
        BootstrapIcons.search,
        'No matching titles',
        'Try a different search in the current library section.',
      );
    }

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
      LibraryView.unknown ||
      LibraryView.watching ||
      LibraryView.completed ||
      LibraryView.onHold ||
      LibraryView.dropped ||
      LibraryView.planned => _FilteredEmptyCopy(
        BootstrapIcons.funnel,
        l10n.libraryFilteredStatusEmptyTitle,
        l10n.libraryFilteredStatusEmptyMessage,
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
