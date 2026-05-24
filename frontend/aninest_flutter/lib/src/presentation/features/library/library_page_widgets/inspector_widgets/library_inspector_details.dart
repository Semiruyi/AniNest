import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_library_section.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_metadata_section.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_title_block.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorDetails extends StatelessWidget {
  const LibraryInspectorDetails({
    super.key,
    required this.folder,
    required this.metadata,
    required this.imageUrl,
  });

  final LibraryFolderDto folder;
  final MetadataDto? metadata;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        LibraryInspectorMetadataSection(folder: folder, metadata: metadata),
        const Gap(12),
        LibraryInspectorLibrarySection(folder: folder),
      ],
    );
  }
}
