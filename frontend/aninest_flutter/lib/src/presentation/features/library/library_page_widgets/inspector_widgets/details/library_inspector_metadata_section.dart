import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_helpers.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_section.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorMetadataSection extends StatelessWidget {
  const LibraryInspectorMetadataSection({
    super.key,
    required this.folder,
    required this.metadata,
  });

  final LibraryFolderDto folder;
  final MetadataDto? metadata;

  @override
  Widget build(BuildContext context) {
    final libraryMetadataTitle = displayMetadataTitle(folder);
    final matchedTitle = metadata?.title ?? libraryMetadataTitle;
    final originalTitle =
        metadata?.originalTitle ?? displayOriginalMetadataTitle(folder);

    return LibraryInspectorSection(
      title: 'Metadata',
      children: <Widget>[
        LibraryInspectorField(
          label: 'Matched title',
          value: matchedTitle ?? 'Not matched yet',
        ),
        if (originalTitle != null && originalTitle != matchedTitle)
          LibraryInspectorField(label: 'Original title', value: originalTitle),
        // LibraryInspectorField(
        //   label: 'State',
        //   value: metadata?.state.name ?? displayMetadataState(folder),
        // ),
        // LibraryInspectorField(
        //   label: 'Matched',
        //   value: summary?.hasMetadata == true ? 'Yes' : 'No',
        // ),
        if (metadata?.episodeCount != null)
          LibraryInspectorField(
            label: 'Episodes',
            value: metadata!.episodeCount.toString(),
          ),
        if (metadata?.airDate != null && metadata!.airDate!.trim().isNotEmpty)
          LibraryInspectorField(label: 'Air date', value: metadata!.airDate!),
        if (metadata?.year != null)
          LibraryInspectorField(
            label: 'Year',
            value: metadata!.year.toString(),
          ),
        if (metadata?.rating != null)
          LibraryInspectorField(
            label: 'Rating',
            value: formatMetadataRating(metadata!.rating),
          ),
        if (metadata?.source != null && metadata!.source!.trim().isNotEmpty)
          LibraryInspectorField(label: 'Source', value: metadata!.source!),
        if (metadata?.tags.isNotEmpty == true)
          LibraryInspectorField(
            label: 'Tags',
            value: formatMetadataTags(metadata!.tags),
          ),
        if (metadata?.summary != null && metadata!.summary!.trim().isNotEmpty)
          LibraryInspectorField(label: 'Summary', value: metadata!.summary!),
        LibraryInspectorField(
          label: 'Favorite',
          value: folder.isFavorite ? 'Yes' : 'No',
        ),
      ],
    );
  }
}
