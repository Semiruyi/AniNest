import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_card_grid.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_empty_state.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryContentPane extends StatelessWidget {
  const LibraryContentPane({super.key, required this.controller});

  final LibraryController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: AnimatedBuilder(
              animation: controller,
              builder: (context, _) {
                if (controller.folders.isEmpty) {
                  return const LibraryEmptyState();
                }

                return LibraryCardGrid(
                  folders: controller.folders,
                  selectedFolderId: controller.selectedFolderId,
                  resolveMediaUrl: controller.resolveMediaUrl,
                  onFolderPressed: controller.selectFolder,
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
