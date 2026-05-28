import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:aninest_flutter/src/features/library/application/library_batch_add_result.dart';
import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter/foundation.dart';

class LibraryController extends ChangeNotifier {
  LibraryController(this._libraryApi);

  LibraryApi _libraryApi;

  List<LibraryFolderDto> _folders = const [];
  String? _selectedFolderId;
  LibraryView _selectedView = LibraryView.allMedia;

  final LibraryViewFilter _viewFilter = const LibraryViewFilter();

  List<LibraryFolderDto> get folders => _folders;
  List<LibraryFolderDto> get visibleFolders =>
      _viewFilter.apply(_selectedView, _folders);
  LibraryView get selectedView => _selectedView;
  String? get selectedFolderId => _selectedFolderId;
  LibraryFolderDto? get selectedFolder {
    final visible = visibleFolders;
    final selectedId = _selectedFolderId;
    if (selectedId == null) {
      return visible.isEmpty ? null : visible.first;
    }

    for (final folder in visible) {
      if (folder.folderId == selectedId) {
        return folder;
      }
    }

    return visible.isEmpty ? null : visible.first;
  }

  void rebind(LibraryApi libraryApi) {
    _libraryApi = libraryApi;
  }

  Future<void> refresh() async {
    final previousSelectedId = _selectedFolderId;
    final nextFolders = await AppPerformanceLogger.measure(
      'Startup.Library',
      'libraryApi.getFolders',
      _libraryApi.getFolders,
    );
    _folders = _mergeRefreshedFolders(nextFolders);
    _selectedFolderId = _resolveSelectedFolderId(previousSelectedId);
    notifyListeners();
  }

  Future<AddLibraryFolderResultDto> addFolder(String path) async {
    final result = await _libraryApi.addFolder(path);
    if (result.isAdded) {
      if (result.folder != null) {
        applyFolderAdded(result.folder!);
        selectFolder(result.folder!.folderId);
      } else {
        await refresh();
        selectFolder(result.folder?.folderId);
      }
    }
    return result;
  }

  Future<LibraryBatchAddResult> addFolderBatch(String rootPath) async {
    final previousById = <String, LibraryFolderDto>{
      for (final folder in _folders) folder.folderId: folder,
    };

    await _libraryApi.addFolderBatch(rootPath);

    final nextFolders = await _libraryApi.getFolders();
    _folders = _mergeRefreshedFolders(nextFolders);

    final addedFolders = _folders
        .where((folder) => !previousById.containsKey(folder.folderId))
        .toList(growable: false);
    final preferredSelectedId =
        _selectedFolderId ??
        (addedFolders.isEmpty ? null : addedFolders.first.folderId);
    _selectedFolderId = _resolveSelectedFolderId(preferredSelectedId);
    notifyListeners();

    return LibraryBatchAddResult(
      rootPath: rootPath,
      addedFolders: addedFolders,
    );
  }

  Future<void> toggleFavorite(String folderId, bool isFavorite) async {
    await _libraryApi.setFavorite(folderId, isFavorite);
    _patchFolder(folderId, (folder) => folder.copyWith(isFavorite: isFavorite));
  }

  Future<void> setWatchStatus(String folderId, WatchStatus status) async {
    await _libraryApi.setWatchStatus(folderId, _encodeWatchStatus(status));
    _patchFolder(folderId, (folder) => folder.copyWith(watchStatus: status));
  }

  Future<void> moveToFront(String folderId) async {
    await _libraryApi.moveToFront(folderId);
    applyFolderReordered(folderId, 0);
    selectFolder(folderId);
  }

  Future<void> deleteFolder(String folderId) async {
    await _libraryApi.deleteFolder(folderId);
    applyFolderRemoved(folderId);
  }

  void selectFolder(String? folderId) {
    final nextSelectedId = _resolveSelectedFolderId(folderId);
    if (nextSelectedId == _selectedFolderId) {
      return;
    }

    _selectedFolderId = nextSelectedId;
    notifyListeners();
  }

  void selectView(LibraryView view) {
    if (view == _selectedView) {
      return;
    }

    _selectedView = view;
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  String? resolveMediaUrl(String? path) => _libraryApi.resolveMediaUrl(path);

  void clear() {
    if (_folders.isEmpty && _selectedFolderId == null) {
      return;
    }

    _folders = const [];
    _selectedFolderId = null;
    notifyListeners();
  }

  void applyMetadataFolderUpdate(MetadataFolderUpdatedEventDto update) {
    var changed = false;
    final nextFolders = _folders
        .map((folder) {
          if (folder.folderId != update.folderId) {
            return folder;
          }

          changed = true;
          final nextMetadata = update.hasMetadata
              ? (folder.metadataSummary ??
                        const LibraryMetadataSummaryDto(
                          matchedTitle: null,
                          originalTitle: null,
                          posterUrl: null,
                          state: 'Unknown',
                          hasMetadata: false,
                        ))
                    .copyWith(
                      matchedTitle: update.matchedTitle,
                      originalTitle: update.originalTitle,
                      posterUrl: update.posterUrl,
                      state: update.state.name,
                      hasMetadata: update.hasMetadata,
                    )
              : null;

          return LibraryFolderDto(
            folderId: folder.folderId,
            name: folder.name,
            path: folder.path,
            videoCount: folder.videoCount,
            coverUrl: update.coverUrl,
            playedCount: folder.playedCount,
            watchStatus: folder.watchStatus,
            isFavorite: folder.isFavorite,
            addedAtUtc: folder.addedAtUtc,
            metadataSummary: nextMetadata,
          );
        })
        .toList(growable: false);

    if (!changed) {
      return;
    }

    _folders = nextFolders;
    notifyListeners();
  }

  void applyFolderAdded(LibraryFolderDto folder) {
    final index = _folders.indexWhere(
      (item) => item.folderId == folder.folderId,
    );
    if (index >= 0) {
      final nextFolders = _folders.toList(growable: false);
      nextFolders[index] = _mergeFolder(_folders[index], folder);
      _folders = nextFolders;
      _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
      notifyListeners();
      return;
    }

    _folders = [..._folders, folder];
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  void applyFolderUpdated(LibraryFolderDto folder) {
    final index = _folders.indexWhere(
      (item) => item.folderId == folder.folderId,
    );
    if (index < 0) {
      applyFolderAdded(folder);
      return;
    }

    final merged = _mergeFolder(_folders[index], folder);
    if (identical(merged, _folders[index])) {
      return;
    }

    final nextFolders = _folders.toList(growable: false);
    nextFolders[index] = merged;
    _folders = nextFolders;
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  void _patchFolder(
    String folderId,
    LibraryFolderDto Function(LibraryFolderDto folder) update,
  ) {
    final index = _folders.indexWhere((item) => item.folderId == folderId);
    if (index < 0) {
      return;
    }

    final nextFolders = _folders.toList(growable: false);
    nextFolders[index] = update(nextFolders[index]);
    _folders = nextFolders;
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  void applyFolderRemoved(String folderId) {
    final nextFolders = _folders
        .where((folder) => folder.folderId != folderId)
        .toList(growable: false);
    if (nextFolders.length == _folders.length) {
      return;
    }

    _folders = nextFolders;
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  void applyFolderReordered(
    String folderId,
    int position, {
    LibraryFolderDto? folder,
  }) {
    final nextFolders = _folders.toList();
    final existingIndex = nextFolders.indexWhere(
      (item) => item.folderId == folderId,
    );
    LibraryFolderDto? item = folder;
    if (existingIndex >= 0) {
      final removed = nextFolders.removeAt(existingIndex);
      item = folder == null ? removed : _mergeFolder(removed, folder);
    }

    if (item == null) {
      return;
    }

    final targetIndex = position.clamp(0, nextFolders.length);
    nextFolders.insert(targetIndex, item);
    _folders = nextFolders.toList(growable: false);
    _selectedFolderId = _resolveSelectedFolderId(_selectedFolderId);
    notifyListeners();
  }

  String? _resolveSelectedFolderId(String? candidateFolderId) {
    final visible = visibleFolders;
    if (visible.isEmpty) {
      return null;
    }

    if (candidateFolderId != null &&
        visible.any((folder) => folder.folderId == candidateFolderId)) {
      return candidateFolderId;
    }

    return visible.first.folderId;
  }

  String _encodeWatchStatus(WatchStatus status) {
    return switch (status) {
      WatchStatus.onHold => 'on_hold',
      _ => status.name,
    };
  }

  List<LibraryFolderDto> _mergeRefreshedFolders(
    List<LibraryFolderDto> nextFolders,
  ) {
    if (_folders.isEmpty || nextFolders.isEmpty) {
      return nextFolders;
    }

    final previousById = <String, LibraryFolderDto>{
      for (final folder in _folders) folder.folderId: folder,
    };

    return nextFolders
        .map((folder) => _mergeFolder(previousById[folder.folderId], folder))
        .toList(growable: false);
  }

  LibraryFolderDto _mergeFolder(
    LibraryFolderDto? previous,
    LibraryFolderDto next,
  ) {
    if (previous == null) {
      return next;
    }

    final nextMetadata = _mergeMetadataSummary(
      previous.metadataSummary,
      next.metadataSummary,
    );
    final path = _preferNonEmpty(next.path, previous.path) ?? '';
    final coverUrl = _preferNonEmpty(next.coverUrl, previous.coverUrl);
    final addedAtUtc = next.addedAtUtc ?? previous.addedAtUtc;

    if (path == next.path &&
        coverUrl == next.coverUrl &&
        nextMetadata == next.metadataSummary &&
        addedAtUtc == next.addedAtUtc) {
      return next;
    }

    return next.copyWith(
      path: path,
      coverUrl: coverUrl,
      addedAtUtc: addedAtUtc,
      metadataSummary: nextMetadata,
    );
  }

  LibraryMetadataSummaryDto? _mergeMetadataSummary(
    LibraryMetadataSummaryDto? previous,
    LibraryMetadataSummaryDto? next,
  ) {
    if (next == null) {
      return previous;
    }

    if (previous == null) {
      return next;
    }

    final matchedTitle = _preferNonEmpty(
      next.matchedTitle,
      previous.matchedTitle,
    );
    final originalTitle = _preferNonEmpty(
      next.originalTitle,
      previous.originalTitle,
    );
    final posterUrl = _preferNonEmpty(next.posterUrl, previous.posterUrl);

    if (matchedTitle == next.matchedTitle &&
        originalTitle == next.originalTitle &&
        posterUrl == next.posterUrl) {
      return next;
    }

    return LibraryMetadataSummaryDto(
      matchedTitle: matchedTitle,
      originalTitle: originalTitle,
      posterUrl: posterUrl,
      state: next.state,
      hasMetadata: next.hasMetadata,
    );
  }

  String? _preferNonEmpty(String? primary, String? fallback) {
    if (primary != null && primary.isNotEmpty) {
      return primary;
    }
    if (fallback != null && fallback.isNotEmpty) {
      return fallback;
    }
    return primary ?? fallback;
  }
}
