import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/app/workflows/library_workflow.dart';
import 'package:aninest_flutter/src/features/library/application/library_batch_add_result.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('addFolder refreshes metadata for added folders', () async {
    final library = _FakeLibraryController()
      ..addFolderResult = AddLibraryFolderResultDto(
        status: 'added',
        message: 'Added',
        reasonCode: null,
        folder: _folder('folder-01'),
      );
    final libraryApi = _FakeLibraryApi();
    final runOperation = _RunOperationSpy();
    final selectionRefresh = _SelectionRefreshSpy();
    final metadataRefreshForces = <bool>[];

    final workflow = LibraryWorkflow(
      library: library,
      libraryApi: libraryApi,
      runOperation: runOperation.call,
      runWithSelectionRefreshSuspended: selectionRefresh.call,
      refreshMetadataForSelection: ({bool force = false}) async {
        metadataRefreshForces.add(force);
      },
      buildAddFolderFailureResult: _buildFailureResult,
      readLastError: () => null,
    );

    final result = await workflow.addFolder('/anime/Folder 01');

    expect(result.usedFailureFallback, isFalse);
    expect(result.result?.isAdded, isTrue);
    expect(library.addFolderPaths, <String>['/anime/Folder 01']);
    expect(runOperation.showSpinners, <bool>[true]);
    expect(selectionRefresh.callCount, 1);
    expect(metadataRefreshForces, <bool>[true]);
  });

  test(
    'addFolder uses failure fallback when operation leaves only lastError',
    () async {
      final library = _FakeLibraryController();
      final libraryApi = _FakeLibraryApi();
      final runOperation = _RunOperationSpy()..invokeOperation = false;
      final selectionRefresh = _SelectionRefreshSpy();
      final builderCalls = <({String path, String errorMessage})>[];

      final workflow = LibraryWorkflow(
        library: library,
        libraryApi: libraryApi,
        runOperation: runOperation.call,
        runWithSelectionRefreshSuspended: selectionRefresh.call,
        refreshMetadataForSelection: ({bool force = false}) async {},
        buildAddFolderFailureResult: (path, errorMessage) {
          builderCalls.add((path: path, errorMessage: errorMessage));
          return _buildFailureResult(path, errorMessage);
        },
        readLastError: () => 'network down',
      );

      final result = await workflow.addFolder('/anime/Folder 02');

      expect(result.usedFailureFallback, isTrue);
      expect(result.result?.isFailed, isTrue);
      expect(
        result.result?.message,
        'Unable to add /anime/Folder 02: network down',
      );
      expect(library.addFolderPaths, isEmpty);
      expect(selectionRefresh.callCount, 0);
      expect(builderCalls, [
        (path: '/anime/Folder 02', errorMessage: 'network down'),
      ]);
    },
  );

  test('refreshLibrary runs without spinner and refreshes metadata', () async {
    final library = _FakeLibraryController();
    final libraryApi = _FakeLibraryApi();
    final runOperation = _RunOperationSpy();
    final selectionRefresh = _SelectionRefreshSpy();
    final metadataRefreshForces = <bool>[];

    final workflow = LibraryWorkflow(
      library: library,
      libraryApi: libraryApi,
      runOperation: runOperation.call,
      runWithSelectionRefreshSuspended: selectionRefresh.call,
      refreshMetadataForSelection: ({bool force = false}) async {
        metadataRefreshForces.add(force);
      },
      buildAddFolderFailureResult: _buildFailureResult,
      readLastError: () => null,
    );

    await workflow.refreshLibrary();

    expect(library.refreshCallCount, 1);
    expect(runOperation.showSpinners, <bool>[false]);
    expect(selectionRefresh.callCount, 1);
    expect(metadataRefreshForces, <bool>[true]);
  });

  test('deleteFolder returns lastError after mutation workflow', () async {
    final library = _FakeLibraryController();
    final libraryApi = _FakeLibraryApi();
    final runOperation = _RunOperationSpy();
    final selectionRefresh = _SelectionRefreshSpy();
    final metadataRefreshForces = <bool>[];

    final workflow = LibraryWorkflow(
      library: library,
      libraryApi: libraryApi,
      runOperation: runOperation.call,
      runWithSelectionRefreshSuspended: selectionRefresh.call,
      refreshMetadataForSelection: ({bool force = false}) async {
        metadataRefreshForces.add(force);
      },
      buildAddFolderFailureResult: _buildFailureResult,
      readLastError: () => 'delete failed',
    );

    final lastError = await workflow.deleteFolder('folder-03');

    expect(lastError, 'delete failed');
    expect(library.deletedFolderIds, <String>['folder-03']);
    expect(runOperation.showSpinners, <bool>[false]);
    expect(selectionRefresh.callCount, 0);
    expect(metadataRefreshForces, <bool>[true]);
  });
}

AddLibraryFolderResultDto _buildFailureResult(
  String path,
  String errorMessage,
) {
  return AddLibraryFolderResultDto(
    status: 'failed',
    message: 'Unable to add $path: $errorMessage',
    reasonCode: 'test_error',
    folder: null,
  );
}

LibraryFolderDto _folder(String folderId) {
  return LibraryFolderDto(
    folderId: folderId,
    name: folderId,
    path: '/anime/$folderId',
    videoCount: 12,
    coverUrl: null,
    playedCount: 0,
    watchStatus: WatchStatus.watching,
    isFavorite: false,
    addedAtUtc: DateTime.utc(2026, 5, 28, 10),
    metadataSummary: null,
  );
}

class _RunOperationSpy {
  final showSpinners = <bool>[];
  bool invokeOperation = true;

  Future<void> call(
    Future<void> Function() operation, {
    bool showSpinner = true,
  }) async {
    showSpinners.add(showSpinner);
    if (invokeOperation) {
      await operation();
    }
  }
}

class _SelectionRefreshSpy {
  int callCount = 0;

  Future<T> call<T>(Future<T> Function() action) async {
    callCount += 1;
    return action();
  }
}

class _FakeLibraryController extends LibraryController {
  _FakeLibraryController() : super(_NoopLibraryApi());

  AddLibraryFolderResultDto? addFolderResult;
  LibraryBatchAddResult? addFolderBatchResult;
  final addFolderPaths = <String>[];
  final addFolderBatchPaths = <String>[];
  final movedFolderIds = <String>[];
  final deletedFolderIds = <String>[];
  final toggledFavorites = <({String folderId, bool isFavorite})>[];
  final watchStatuses = <({String folderId, WatchStatus status})>[];
  final selectedFolderIds = <String?>[];
  int refreshCallCount = 0;

  @override
  Future<AddLibraryFolderResultDto> addFolder(String path) async {
    addFolderPaths.add(path);
    return addFolderResult!;
  }

  @override
  Future<LibraryBatchAddResult> addFolderBatch(String rootPath) async {
    addFolderBatchPaths.add(rootPath);
    return addFolderBatchResult!;
  }

  @override
  Future<void> refresh() async {
    refreshCallCount += 1;
  }

  @override
  Future<void> moveToFront(String folderId) async {
    movedFolderIds.add(folderId);
  }

  @override
  Future<void> deleteFolder(String folderId) async {
    deletedFolderIds.add(folderId);
  }

  @override
  Future<void> toggleFavorite(String folderId, bool isFavorite) async {
    toggledFavorites.add((folderId: folderId, isFavorite: isFavorite));
  }

  @override
  Future<void> setWatchStatus(String folderId, WatchStatus status) async {
    watchStatuses.add((folderId: folderId, status: status));
  }

  @override
  void selectFolder(String? folderId) {
    selectedFolderIds.add(folderId);
  }
}

class _FakeLibraryApi extends LibraryApi {
  _FakeLibraryApi()
    : super(AniNestHttpClient(baseUrl: 'http://localhost:5275'));

  String? browsedPath;
  LibraryBrowserResponse? browseResponse;

  @override
  Future<LibraryBrowserResponse> browse(String? path) async {
    browsedPath = path;
    return browseResponse ??
        const LibraryBrowserResponse(
          rootPath: '/',
          currentPath: '/',
          parentPath: null,
          canSelect: true,
          directories: <LibraryBrowserDirectoryDto>[],
        );
  }
}

class _NoopLibraryApi extends LibraryApi {
  _NoopLibraryApi()
    : super(AniNestHttpClient(baseUrl: 'http://localhost:5275'));
}
