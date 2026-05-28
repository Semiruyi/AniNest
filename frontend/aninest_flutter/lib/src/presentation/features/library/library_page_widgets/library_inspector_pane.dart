import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/library_inspector_details.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/library_inspector_empty_state.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorPane extends StatelessWidget {
  const LibraryInspectorPane({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.card,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(kLibraryPanePadding),
        child: ListenableBuilder(
          listenable: controller,
          builder: (context, _) {
            final folder = controller.selectedFolder;

            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                if (folder == null)
                  const LibraryInspectorEmptyState()
                else
                  LibraryInspectorDetails(
                    folder: folder,
                    metadata: controller.selectedMetadata,
                    imageUrl: controller.resolveMediaUrl(
                      folder.coverUrl ?? folder.metadataSummary?.posterUrl,
                    ),
                  ),
              ],
            );
          },
        ),
      ),
    );
  }
}
