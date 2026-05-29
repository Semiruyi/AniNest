import 'package:aninest_flutter/src/models/enums.dart';
import 'dart:math' as math;

import 'package:animated_containers/animated_containers.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_folder_card.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryCardGrid extends StatelessWidget {
  const LibraryCardGrid({
    super.key,
    required this.folders,
    required this.selectedFolderId,
    required this.resolveMediaUrl,
    required this.onFolderPressed,
    required this.onOpen,
    required this.onToggleFavorite,
    required this.onSetWatchStatus,
    required this.onMoveToFront,
    required this.onDelete,
  });

  final List<LibraryFolderDto> folders;
  final String? selectedFolderId;
  final String? Function(String? path) resolveMediaUrl;
  final ValueChanged<String?> onFolderPressed;
  final ValueChanged<String> onOpen;
  final void Function(String folderId, bool isFavorite) onToggleFavorite;
  final void Function(String folderId, WatchStatus status) onSetWatchStatus;
  final ValueChanged<String> onMoveToFront;
  final ValueChanged<String> onDelete;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        const gap = 25.0;
        final cardWidth = _resolveCardWidth(constraints.maxWidth, gap);

        return SingleChildScrollView(
          padding: const EdgeInsets.only(bottom: 16),
          child: AnimatedWrap.material3(
            spacing: gap,
            runSpacing: gap,
            alignment: WrapAlignment.start,
            runAlignment: WrapAlignment.start,
            staggeredInitialInsertionAnimation: const Duration(
              milliseconds: 35,
            ),
            children: folders
                .map(
                  (folder) => SizedBox(
                    key: ValueKey(folder.folderId),
                    width: cardWidth,
                    child: LibraryFolderCard(
                      folder: folder,
                      imageUrl: resolveMediaUrl(
                        folder.coverUrl ?? folder.metadataSummary?.posterUrl,
                      ),
                      isSelected: selectedFolderId == folder.folderId,
                      onPressed: () => onFolderPressed(folder.folderId),
                      onContextMenuRequested: () =>
                          onFolderPressed(folder.folderId),
                      onOpen: () => onOpen(folder.folderId),
                      onToggleFavorite: (isFavorite) =>
                          onToggleFavorite(folder.folderId, isFavorite),
                      onSetWatchStatus: (status) =>
                          onSetWatchStatus(folder.folderId, status),
                      onMoveToFront: () => onMoveToFront(folder.folderId),
                      onDelete: () => onDelete(folder.folderId),
                    ),
                  ),
                )
                .toList(),
          ),
        );
      },
    );
  }

  double _resolveCardWidth(double maxWidth, double gap) {
    const minCardWidth = 210.0;
    const maxCardWidth = 280.0;

    final columns = math.max(
      1,
      ((maxWidth + gap) / (minCardWidth + gap)).floor(),
    );
    final width = (maxWidth - (columns - 1) * gap) / columns;
    return width.clamp(minCardWidth, maxCardWidth);
  }
}
