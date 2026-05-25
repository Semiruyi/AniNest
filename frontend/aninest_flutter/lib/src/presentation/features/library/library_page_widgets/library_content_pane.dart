import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_card_grid.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_empty_state.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryContentPane extends StatelessWidget {
  const LibraryContentPane({super.key, required this.controller});

  final AppController controller;

  Future<void> _handleOpen(BuildContext context, String folderId) async {
    controller.selectFolder(folderId);
    final error = await controller.openLibraryFolder(folderId);
    if (context.mounted && error != null) {
      await _showOperationError(context, message: error);
    }
  }

  Future<void> _handleToggleFavorite(
    BuildContext context,
    String folderId,
    bool isFavorite,
  ) async {
    controller.selectFolder(folderId);
    final error = await controller.toggleFolderFavorite(folderId, isFavorite);
    if (context.mounted && error != null) {
      await _showOperationError(context, message: error);
    }
  }

  Future<void> _handleSetWatchStatus(
    BuildContext context,
    String folderId,
    WatchStatus status,
  ) async {
    controller.selectFolder(folderId);
    final error = await controller.setFolderWatchStatus(folderId, status);
    if (context.mounted && error != null) {
      await _showOperationError(context, message: error);
    }
  }

  Future<void> _handleMoveToFront(BuildContext context, String folderId) async {
    controller.selectFolder(folderId);
    final error = await controller.moveFolderToFront(folderId);
    if (context.mounted && error != null) {
      await _showOperationError(context, message: error);
    }
  }

  Future<void> _handleDelete(BuildContext context, String folderId) async {
    final l10n = AppLocalizations.of(context);
    controller.selectFolder(folderId);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.libraryCardDeleteDialogTitle),
        content: Text(l10n.libraryCardDeleteDialogMessage),
        actions: <Widget>[
          SecondaryButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.commonCancel),
          ),
          DestructiveButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.commonDelete),
          ),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }

    final error = await controller.deleteFolder(folderId);
    if (context.mounted && error != null) {
      await _showOperationError(context, message: error);
    }
  }

  Future<void> _showOperationError(
    BuildContext context, {
    required String message,
  }) {
    final l10n = AppLocalizations.of(context);
    return showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.commonOperationFailed),
        content: Text(message),
        actions: <Widget>[
          PrimaryButton(
            onPressed: () => Navigator.of(context).pop(),
            child: Text(l10n.commonClose),
          ),
        ],
      ),
    );
  }

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
              animation: controller.library,
              builder: (context, _) {
                if (controller.library.folders.isEmpty) {
                  return const LibraryEmptyState();
                }

                return LibraryCardGrid(
                  folders: controller.library.folders,
                  selectedFolderId: controller.library.selectedFolderId,
                  resolveMediaUrl: controller.library.resolveMediaUrl,
                  onFolderPressed: controller.selectFolder,
                  onOpen: (folderId) => _handleOpen(context, folderId),
                  onToggleFavorite: (folderId, isFavorite) =>
                      _handleToggleFavorite(context, folderId, isFavorite),
                  onSetWatchStatus: (folderId, status) =>
                      _handleSetWatchStatus(context, folderId, status),
                  onMoveToFront: (folderId) =>
                      _handleMoveToFront(context, folderId),
                  onDelete: (folderId) => _handleDelete(context, folderId),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
