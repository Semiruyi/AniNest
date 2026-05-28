import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_section.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorLibrarySection extends StatelessWidget {
  const LibraryInspectorLibrarySection({super.key, required this.folder});

  final LibraryFolderDto folder;

  @override
  Widget build(BuildContext context) {
    return LibraryInspectorSection(
      title: 'Library',
      children: <Widget>[
        LibraryInspectorField(label: 'Folder name', value: folder.name),
        if (folder.path.isNotEmpty)
          LibraryInspectorField(label: 'Path', value: folder.path),
        LibraryInspectorField(
          label: 'Episodes',
          value: folder.videoCount <= 0
              ? 'No episodes detected'
              : '${folder.videoCount} detected',
        ),
        LibraryInspectorField(
          label: 'Progress',
          value: folder.videoCount <= 0
              ? 'No playback history'
              : '${folder.playedCount} / ${folder.videoCount} watched',
        ),
        LibraryInspectorField(label: 'Folder ID', value: folder.folderId),
      ],
    );
  }
}
