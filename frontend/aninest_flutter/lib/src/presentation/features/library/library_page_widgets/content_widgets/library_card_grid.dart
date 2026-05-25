import 'dart:math' as math;

import 'package:aninest_flutter/src/core/logging/app_logger.dart';
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
  });

  final List<LibraryFolderDto> folders;
  final String? selectedFolderId;
  final String? Function(String? path) resolveMediaUrl;
  final ValueChanged<String?> onFolderPressed;

  static String? _lastRenderSignature;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final cardWidth = _resolveCardWidth(constraints.maxWidth);
        _logRenderSnapshot();

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
                    ),
                  ),
                )
                .toList(),
          ),
        );
      },
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

  void _logRenderSnapshot() {
    final resolved = folders
        .map(
          (folder) => (
            folder: folder,
            imageUrl: resolveMediaUrl(
              folder.coverUrl ?? folder.metadataSummary?.posterUrl,
            ),
          ),
        )
        .toList(growable: false);
    final withImage = resolved
        .where((entry) => entry.imageUrl?.isNotEmpty ?? false)
        .length;
    final missing = resolved
        .where((entry) => !(entry.imageUrl?.isNotEmpty ?? false))
        .map((entry) => '${entry.folder.folderId}:${entry.folder.name}')
        .take(5)
        .join(', ');
    final signature =
        'count=${folders.length}|selected=$selectedFolderId|withImage=$withImage|missing=${folders.length - withImage}|ids=$missing';

    if (signature == _lastRenderSignature) {
      return;
    }

    _lastRenderSignature = signature;
    AppLogger.info('LibraryCardGrid.Render', signature);
  }
}
