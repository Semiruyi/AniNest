import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_helpers.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorTitleBlock extends StatelessWidget {
  const LibraryInspectorTitleBlock({
    super.key,
    required this.folder,
  });

  final LibraryFolderDto folder;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final metadataTitle = folder.metadataSummary?.title;

    return SurfaceCard(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  displayLibraryFolderTitle(folder),
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                ).semiBold(),
              ),
              if (folder.isFavorite) ...<Widget>[
                const Gap(8),
                Icon(
                  BootstrapIcons.heartFill,
                  size: 14,
                  color: colorScheme.destructive,
                ),
              ],
            ],
          ),
          if (metadataTitle == null || metadataTitle.trim().isEmpty) ...<Widget>[
            const Gap(6),
            Text(
              'Metadata title not available yet',
              style: TextStyle(
                fontSize: 13,
                color: colorScheme.mutedForeground,
              ),
            ),
          ] else if (metadataTitle != folder.name) ...<Widget>[
            const Gap(6),
            Text(
              folder.name,
              style: TextStyle(
                fontSize: 13,
                color: colorScheme.mutedForeground,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
