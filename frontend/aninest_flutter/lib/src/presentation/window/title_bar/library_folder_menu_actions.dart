import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/features/library/application/library_batch_add_result.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_controller.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_models.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/server_folder_browser_dialog.dart';
import 'package:flutter/widgets.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryFolderMenuActions {
  const LibraryFolderMenuActions({
    required this.controller,
    required this.feedbackController,
  });

  final AppController controller;
  final AppFeedbackController feedbackController;

  Future<void> handleAddFolder(BuildContext context) async {
    final l10n = AppLocalizations.of(context);

    try {
      final path = await _selectFolder(context);
      if (path == null || path.isEmpty) {
        return;
      }

      final result = await controller.addFolder(path);
      if (result == null) {
        AppLogger.warning(
          'LibraryFolderMenuActions.AddFolder',
          'Received null addFolder result.',
        );
        return;
      }

      if (result.isAdded) {
        return;
      }

      final folderName = _folderDisplayName(path, result.folder?.name);
      if (result.isAlreadyExists) {
        feedbackController.publish(
          AppFeedbackRequest(
            kind: AppFeedbackKind.toastInfo,
            title: l10n.addFolderAlreadyAddedTitle,
            message: l10n.addFolderAlreadyAddedMessage(folderName),
          ),
        );
        return;
      }

      feedbackController.publish(
        AppFeedbackRequest(
          kind: AppFeedbackKind.dialogError,
          title: l10n.addFolderErrorTitle,
          message: result.message,
        ),
      );
    } catch (error, stackTrace) {
      AppLogger.error(
        'LibraryFolderMenuActions.AddFolder',
        'Unhandled exception while processing addFolder.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<void> handleScanFolder(BuildContext context) async {
    final l10n = AppLocalizations.of(context);

    try {
      final path = await _selectFolder(context);
      if (path == null || path.isEmpty) {
        return;
      }

      final result = await controller.scanFolder(path);
      if (result == null) {
        final message =
            controller.lastError ??
            l10n.scanFolderFallbackErrorMessage(_folderDisplayName(path, null));
        feedbackController.publish(
          AppFeedbackRequest(
            kind: AppFeedbackKind.dialogError,
            title: l10n.scanFolderErrorTitle,
            message: message,
          ),
        );
        return;
      }

      _publishScanResult(l10n, result);
    } catch (error, stackTrace) {
      AppLogger.error(
        'LibraryFolderMenuActions.ScanFolder',
        'Unhandled exception while processing scanFolder.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<String?> _selectFolder(BuildContext context) {
    return showDialog<String>(
      context: context,
      builder: (context) => ServerFolderBrowserDialog(controller: controller),
    );
  }

  void _publishScanResult(AppLocalizations l10n, LibraryBatchAddResult result) {
    final rootName = _folderDisplayName(result.rootPath, null);
    if (!result.hasAddedFolders) {
      feedbackController.publish(
        AppFeedbackRequest(
          kind: AppFeedbackKind.toastInfo,
          title: l10n.scanFolderNoNewFoldersTitle,
          message: l10n.scanFolderNoNewFoldersMessage(rootName),
        ),
      );
      return;
    }

    feedbackController.publish(
      AppFeedbackRequest(
        kind: AppFeedbackKind.toastInfo,
        title: l10n.scanFolderSuccessTitle,
        message: l10n.scanFolderSuccessMessage(result.addedCount, rootName),
      ),
    );
  }

  String _folderDisplayName(String path, String? fallbackName) {
    if (fallbackName != null && fallbackName.isNotEmpty) {
      return fallbackName;
    }

    final normalizedPath = path.replaceAll('\\', '/');
    final name = normalizedPath.split('/').last;
    return name.isEmpty ? path : name;
  }
}
