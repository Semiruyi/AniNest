import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryNavigationPane extends StatelessWidget {
  const LibraryNavigationPane({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.card,
      child: AnimatedBuilder(
        animation: controller,
        builder: (context, _) {
          return SingleChildScrollView(
            padding: const EdgeInsets.all(kLibraryPanePadding),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _NavGroup(
                  title: l10n.libraryNavLibraryTitle,
                  selectedView: controller.libraryView,
                  onViewSelected: controller.selectLibraryView,
                  items: <_NavItemData>[
                    _NavItemData(
                      l10n.libraryViewAllMedia,
                      BootstrapIcons.collectionPlay,
                      LibraryView.allMedia,
                    ),
                    _NavItemData(
                      l10n.libraryViewRecentlyAdded,
                      BootstrapIcons.clockHistory,
                      LibraryView.recentlyAdded,
                    ),
                    _NavItemData(
                      l10n.libraryViewContinueWatching,
                      BootstrapIcons.playCircle,
                      LibraryView.continueWatching,
                    ),
                    _NavItemData(
                      l10n.libraryViewFavorites,
                      BootstrapIcons.heart,
                      LibraryView.favorites,
                    ),
                  ],
                ),
                const Gap(12),
                _NavGroup(
                  title: l10n.libraryNavWatchStatusTitle,
                  selectedView: controller.libraryView,
                  onViewSelected: controller.selectLibraryView,
                  items: <_NavItemData>[
                    _NavItemData(
                      l10n.watchStatusUnknown,
                      BootstrapIcons.questionCircle,
                      LibraryView.unknown,
                    ),
                    _NavItemData(
                      l10n.watchStatusWatching,
                      BootstrapIcons.playCircle,
                      LibraryView.watching,
                    ),
                    _NavItemData(
                      l10n.watchStatusCompleted,
                      BootstrapIcons.checkCircle,
                      LibraryView.completed,
                    ),
                    _NavItemData(
                      l10n.watchStatusOnHold,
                      BootstrapIcons.pauseCircle,
                      LibraryView.onHold,
                    ),
                    _NavItemData(
                      l10n.watchStatusDropped,
                      BootstrapIcons.xCircle,
                      LibraryView.dropped,
                    ),
                    _NavItemData(
                      l10n.watchStatusPlanned,
                      BootstrapIcons.bookmark,
                      LibraryView.planned,
                    ),
                  ],
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _NavGroup extends StatelessWidget {
  const _NavGroup({
    required this.title,
    required this.items,
    required this.selectedView,
    required this.onViewSelected,
  });

  final String title;
  final List<_NavItemData> items;
  final LibraryView selectedView;
  final ValueChanged<LibraryView> onViewSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SurfaceCard(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            title,
            style: TextStyle(fontSize: 13, color: colorScheme.mutedForeground),
          ),
          const Gap(8),
          for (final item in items) ...<Widget>[
            _LibraryNavItem(
              data: item,
              selected: item.view == selectedView,
              onSelected: onViewSelected,
            ),
            const Gap(4),
          ],
        ],
      ),
    );
  }
}

class _LibraryNavItem extends StatelessWidget {
  const _LibraryNavItem({
    required this.data,
    required this.selected,
    required this.onSelected,
  });

  final _NavItemData data;
  final bool selected;
  final ValueChanged<LibraryView> onSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final view = data.view;
    final isEnabled = view != null;

    return MouseRegion(
      cursor: isEnabled ? SystemMouseCursors.click : MouseCursor.defer,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: view == null ? null : () => onSelected(view),
        child: Container(
          height: 36,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: selected ? colorScheme.secondary : null,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Row(
            children: <Widget>[
              Icon(
                data.icon,
                size: 16,
                color: selected
                    ? colorScheme.foreground
                    : colorScheme.mutedForeground,
              ),
              const Gap(8),
              Expanded(
                child: Text(
                  data.label,
                  style: TextStyle(
                    color: selected
                        ? colorScheme.foreground
                        : colorScheme.mutedForeground,
                  ),
                ),
              ),
              if (selected)
                Container(
                  width: 6,
                  height: 6,
                  decoration: BoxDecoration(
                    color: colorScheme.primary,
                    shape: BoxShape.circle,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavItemData {
  const _NavItemData(this.label, this.icon, [this.view]);

  final String label;
  final IconData icon;
  final LibraryView? view;
}
