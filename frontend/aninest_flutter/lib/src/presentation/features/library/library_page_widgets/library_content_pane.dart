import 'dart:math' as math;

import 'package:animated_containers/animated_containers.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryContentPane extends StatelessWidget {
  const LibraryContentPane({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const LibraryPaneHeader(
            title: 'All Media',
            subtitle: '128 items • Animated wrap view',
          ),
          const Gap(12),
          Expanded(
            child: LayoutBuilder(
              builder: (context, constraints) {
                final cardWidth = _resolveCardWidth(constraints.maxWidth);

                return SingleChildScrollView(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: AnimatedWrap.material3(
                    spacing: 12,
                    runSpacing: 12,
                    alignment: WrapAlignment.start,
                    runAlignment: WrapAlignment.start,
                    staggeredInitialInsertionAnimation: const Duration(
                      milliseconds: 35,
                    ),
                    children: List<Widget>.generate(
                      12,
                      (index) => SizedBox(
                        key: ValueKey('library-card-$index'),
                        width: cardWidth,
                        child: _LibraryCard(index: index),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  double _resolveCardWidth(double maxWidth) {
    const spacing = 12.0;
    const minCardWidth = 210.0;
    const maxCardWidth = 280.0;

    final columns = math.max(
      1,
      ((maxWidth + spacing) / (minCardWidth + spacing)).floor(),
    );
    final width = (maxWidth - (columns - 1) * spacing) / columns;
    return width.clamp(minCardWidth, maxCardWidth);
  }
}

class _LibraryCard extends StatelessWidget {
  const _LibraryCard({required this.index});

  final int index;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SurfaceCard(
      child: SizedBox(
        height: 312,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Expanded(
                child: Container(
                  decoration: BoxDecoration(
                    color: colorScheme.secondary,
                    borderRadius: BorderRadius.circular(10),
                  ),
                  alignment: Alignment.center,
                  child: Icon(
                    BootstrapIcons.image,
                    size: 28,
                    color: colorScheme.mutedForeground,
                  ),
                ),
              ),
              const Gap(10),
              Text('Media Title ${index + 1}').semiBold(),
              const Gap(4),
              Text(
                'Season 1 • 12 Episodes',
                style: TextStyle(
                  fontSize: 13,
                  color: colorScheme.mutedForeground,
                ),
              ),
              const Gap(8),
              Container(
                height: 6,
                decoration: BoxDecoration(
                  color: colorScheme.secondary,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: FractionallySizedBox(
                  alignment: Alignment.centerLeft,
                  widthFactor: ((index % 5) + 1) / 5,
                  child: Container(
                    decoration: BoxDecoration(
                      color: colorScheme.primary,
                      borderRadius: BorderRadius.circular(999),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
