import 'package:aninest_flutter/src/features/library/application/library_batch_add_result.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';

typedef RunAppOperation =
    Future<void> Function(
      Future<void> Function() operation, {
      bool showSpinner,
    });
typedef RunWithSelectionRefreshSuspended =
    Future<T> Function<T>(Future<T> Function() action);
typedef RefreshMetadataSelection = Future<void> Function({bool force});
typedef BuildAddFolderFailureResult =
    AddLibraryFolderResultDto Function(String path, String errorMessage);
typedef ReadLastError = String? Function();

class AddFolderWorkflowResult {
  const AddFolderWorkflowResult({
    required this.result,
    required this.usedFailureFallback,
  });

  final AddLibraryFolderResultDto? result;
  final bool usedFailureFallback;
}

class LibraryWorkflow {
  LibraryWorkflow({
    required LibraryController library,
    required LibraryApi libraryApi,
    required RunAppOperation runOperation,
    required RunWithSelectionRefreshSuspended runWithSelectionRefreshSuspended,
    required RefreshMetadataSelection refreshMetadataForSelection,
    required BuildAddFolderFailureResult buildAddFolderFailureResult,
    required ReadLastError readLastError,
  }) : _library = library,
       _libraryApi = libraryApi,
       _runOperation = runOperation,
       _runWithSelectionRefreshSuspended = runWithSelectionRefreshSuspended,
       _refreshMetadataForSelection = refreshMetadataForSelection,
       _buildAddFolderFailureResult = buildAddFolderFailureResult,
       _readLastError = readLastError;

  final LibraryController _library;
  final LibraryApi _libraryApi;
  final RunAppOperation _runOperation;
  final RunWithSelectionRefreshSuspended _runWithSelectionRefreshSuspended;
  final RefreshMetadataSelection _refreshMetadataForSelection;
  final BuildAddFolderFailureResult _buildAddFolderFailureResult;
  final ReadLastError _readLastError;

  Future<AddFolderWorkflowResult> addFolder(String path) async {
    AddLibraryFolderResultDto? result;
    await _runOperation(() async {
      result = await _runWithSelectionRefreshSuspended(
        () => _library.addFolder(path),
      );
      if (result?.isAdded ?? false) {
        await _refreshMetadataForSelection(force: true);
      }
    });
    final lastError = _readLastError();
    if (result == null && lastError != null) {
      return AddFolderWorkflowResult(
        result: _buildAddFolderFailureResult(path, lastError),
        usedFailureFallback: true,
      );
    }

    return AddFolderWorkflowResult(result: result, usedFailureFallback: false);
  }

  Future<LibraryBrowserResponse> browseDirectory(String? path) {
    return _libraryApi.browse(path);
  }

  Future<LibraryBatchAddResult?> scanFolder(String rootPath) async {
    LibraryBatchAddResult? result;
    await _runOperation(() async {
      result = await _runWithSelectionRefreshSuspended(
        () => _library.addFolderBatch(rootPath),
      );
      await _refreshMetadataForSelection(force: true);
    });
    return result;
  }

  Future<void> refreshLibrary() async {
    await _runOperation(() async {
      await _runWithSelectionRefreshSuspended(_library.refresh);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
  }

  Future<String?> moveFolderToFront(String folderId) async {
    await _runOperation(() async {
      await _library.moveToFront(folderId);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
    return _readLastError();
  }

  Future<String?> deleteFolder(String folderId) async {
    await _runOperation(() async {
      await _library.deleteFolder(folderId);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
    return _readLastError();
  }

  Future<String?> toggleFolderFavorite(String folderId, bool isFavorite) async {
    await _runOperation(() async {
      await _library.toggleFavorite(folderId, isFavorite);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
    return _readLastError();
  }

  Future<String?> setFolderWatchStatus(
    String folderId,
    WatchStatus status,
  ) async {
    await _runOperation(() async {
      await _library.setWatchStatus(folderId, status);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
    return _readLastError();
  }

  void selectFolder(String? folderId) {
    _library.selectFolder(folderId);
  }
}
