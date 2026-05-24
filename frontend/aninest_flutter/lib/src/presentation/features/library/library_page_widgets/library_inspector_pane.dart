import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorPane extends StatelessWidget {
  const LibraryInspectorPane({super.key});

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
            LibraryPaneHeader(
              title: 'Inspector',
              subtitle: 'Selection and filters',
            ),
            Gap(12),
            _InspectorSection(
              title: 'Active Search',
              child: _InfoPlaceholder(lines: <String>[
                'Keyword: Empty',
                'Type: Any',
                'Sort: Recently added',
              ]),
            ),
            Gap(12),
            _InspectorSection(
              title: 'Selected Item',
              child: _SelectedItemPlaceholder(),
            ),
            Gap(12),
            _InspectorSection(
              title: 'Quick Actions',
              child: _ActionListPlaceholder(),
            ),
          ],
        ),
      ),
    );
  }
}

class _InspectorSection extends StatelessWidget {
  const _InspectorSection({
    required this.title,
    required this.child,
  });

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SurfaceCard(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(title).semiBold(),
          const Gap(10),
          child,
        ],
      ),
    );
  }
}

class _InfoPlaceholder extends StatelessWidget {
  const _InfoPlaceholder({required this.lines});

  final List<String> lines;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final line in lines) ...<Widget>[
          Text(
            line,
            style: TextStyle(color: colorScheme.mutedForeground),
          ),
          const Gap(6),
        ],
      ],
    );
  }
}

class _SelectedItemPlaceholder extends StatelessWidget {
  const _SelectedItemPlaceholder();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        AspectRatio(
          aspectRatio: 16 / 9,
          child: Container(
            decoration: BoxDecoration(
              color: colorScheme.secondary,
              borderRadius: BorderRadius.circular(10),
            ),
            alignment: Alignment.center,
            child: Icon(
              BootstrapIcons.collectionPlay,
              size: 32,
              color: colorScheme.mutedForeground,
            ),
          ),
        ),
        const Gap(10),
        const Text('Nothing selected').semiBold(),
        const Gap(4),
        Text(
          'Pick a card in the content area to preview metadata, progress and actions here.',
          style: TextStyle(
            fontSize: 13,
            color: colorScheme.mutedForeground,
          ),
        ),
      ],
    );
  }
}

class _ActionListPlaceholder extends StatelessWidget {
  const _ActionListPlaceholder();

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        GhostButton(
          onPressed: () {},
          leading: const Icon(BootstrapIcons.arrowClockwise),
          child: const Align(
            alignment: Alignment.centerLeft,
            child: Text('Refresh Library'),
          ),
        ),
        const Gap(6),
        GhostButton(
          onPressed: () {},
          leading: const Icon(BootstrapIcons.pencilSquare),
          child: const Align(
            alignment: Alignment.centerLeft,
            child: Text('Edit Metadata'),
          ),
        ),
        const Gap(6),
        GhostButton(
          onPressed: () {},
          leading: const Icon(BootstrapIcons.playFill),
          child: const Align(
            alignment: Alignment.centerLeft,
            child: Text('Play Selected'),
          ),
        ),
      ],
    );
  }
}
