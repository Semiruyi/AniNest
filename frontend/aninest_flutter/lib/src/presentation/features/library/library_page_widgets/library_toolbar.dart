import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryToolbar extends StatelessWidget {
  const LibraryToolbar({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: kLibraryToolbarHeight,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      decoration: BoxDecoration(
        color: colorScheme.background,
        border: Border(bottom: BorderSide(color: colorScheme.border)),
      ),
      child: Row(
        children: <Widget>[
          const Text('Media Library').semiBold(),
          const Gap(16),
          const Expanded(child: _SearchPlaceholder()),
          const Gap(12),
          GhostButton(
            onPressed: () {},
            leading: const Icon(RadixIcons.mixerHorizontal),
            child: const Text('Filter'),
          ),
          const Gap(8),
          GhostButton(
            onPressed: () {},
            leading: const Icon(BootstrapIcons.sortDown),
            child: const Text('Sort'),
          ),
          const Gap(8),
          GhostButton(
            onPressed: () {},
            leading: const Icon(BootstrapIcons.grid3x3Gap),
            child: const Text('Grid'),
          ),
        ],
      ),
    );
  }
}

class _SearchPlaceholder extends StatelessWidget {
  const _SearchPlaceholder();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: 40,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: colorScheme.card,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: colorScheme.border),
      ),
      child: Row(
        children: <Widget>[
          Icon(
            RadixIcons.magnifyingGlass,
            size: 16,
            color: colorScheme.mutedForeground,
          ),
          const Gap(8),
          Text(
            'Search title, tags, folders...',
            style: TextStyle(color: colorScheme.mutedForeground),
          ),
        ],
      ),
    );
  }
}
