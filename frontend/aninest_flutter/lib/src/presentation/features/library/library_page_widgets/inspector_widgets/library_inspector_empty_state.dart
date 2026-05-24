import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorEmptyState extends StatelessWidget {
  const LibraryInspectorEmptyState({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SurfaceCard(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            BootstrapIcons.infoCircle,
            size: 18,
            color: colorScheme.mutedForeground,
          ),
          const Gap(12),
          const Text('No selection').semiBold(),
          const Gap(4),
          Text(
            'Select a title in the library to inspect its cover art and metadata.',
            style: TextStyle(
              fontSize: 13,
              color: colorScheme.mutedForeground,
            ),
          ),
        ],
      ),
    );
  }
}
