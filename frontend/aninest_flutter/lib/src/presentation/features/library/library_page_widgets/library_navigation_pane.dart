import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryNavigationPane extends StatelessWidget {
  const LibraryNavigationPane({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.card,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(kLibraryPanePadding),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: const <Widget>[
            _NavGroup(
              title: 'Library',
              items: <_NavItemData>[
                _NavItemData('All Media', BootstrapIcons.collectionPlay, true),
                _NavItemData('Recently Added', BootstrapIcons.clockHistory),
                _NavItemData('Continue Watching', BootstrapIcons.playCircle),
                _NavItemData('Favorites', BootstrapIcons.heart),
              ],
            ),
            Gap(12),
            _NavGroup(
              title: 'Type',
              items: <_NavItemData>[
                _NavItemData('Series', BootstrapIcons.tv),
                _NavItemData('Movies', BootstrapIcons.film),
                _NavItemData('OVA / Specials', BootstrapIcons.collection),
              ],
            ),
            Gap(12),
            _NavGroup(
              title: 'Sources',
              items: <_NavItemData>[
                _NavItemData('Folders', BootstrapIcons.folder2Open),
                _NavItemData('Tags', BootstrapIcons.tags),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _NavGroup extends StatelessWidget {
  const _NavGroup({
    required this.title,
    required this.items,
  });

  final String title;
  final List<_NavItemData> items;

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
            style: TextStyle(
              fontSize: 13,
              color: colorScheme.mutedForeground,
            ),
          ),
          const Gap(8),
          for (final item in items) ...<Widget>[
            _LibraryNavItem(data: item),
            const Gap(4),
          ],
        ],
      ),
    );
  }
}

class _LibraryNavItem extends StatelessWidget {
  const _LibraryNavItem({required this.data});

  final _NavItemData data;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: 36,
      padding: const EdgeInsets.symmetric(horizontal: 10),
      decoration: BoxDecoration(
        color: data.selected ? colorScheme.secondary : null,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: <Widget>[
          Icon(
            data.icon,
            size: 16,
            color: data.selected
                ? colorScheme.foreground
                : colorScheme.mutedForeground,
          ),
          const Gap(8),
          Expanded(
            child: Text(
              data.label,
              style: TextStyle(
                color: data.selected
                    ? colorScheme.foreground
                    : colorScheme.mutedForeground,
              ),
            ),
          ),
          if (data.selected)
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
    );
  }
}

class _NavItemData {
  const _NavItemData(this.label, this.icon, [this.selected = false]);

  final String label;
  final IconData icon;
  final bool selected;
}
