import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_helpers.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_section.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorMetadataSection extends StatelessWidget {
  const LibraryInspectorMetadataSection({
    super.key,
    required this.folder,
  });

  final LibraryFolderDto folder;

  @override
  Widget build(BuildContext context) {
    final metadata = folder.metadataSummary;

    return LibraryInspectorSection(
      title: 'Metadata',
      children: <Widget>[
        LibraryInspectorField(
          label: 'Title',
          value: metadata?.title ?? 'Not matched yet',
        ),
        LibraryInspectorField(
          label: 'Watch status',
          value: watchStatusLabel(folder.watchStatus),
        ),
        LibraryInspectorField(
          label: 'Favorite',
          value: folder.isFavorite ? 'Yes' : 'No',
        ),
      ],
    );
  }
}
