import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter/foundation.dart';

class LibraryController extends ChangeNotifier {
  LibraryController(this._libraryApi);

  LibraryApi _libraryApi;

  List<LibraryFolderDto> _folders = const [];
  String? _selectedFolderId;

  List<LibraryFolderDto> get folders => _folders;
  String? get selectedFolderId => _selectedFolderId;
  LibraryFolderDto? get selectedFolder {
    final selectedId = _selectedFolderId;
    if (selectedId == null) {
      return _folders.isEmpty ? null : _folders.first;
    }

    for (final folder in _folders) {
      if (folder.folderId == selectedId) {
        return folder;
      }
    }

    return _folders.isEmpty ? null : _folders.first;
  }

  void rebind(LibraryApi libraryApi) {
    _libraryApi = libraryApi;
  }

  Future<void> refresh() async {
    final previousSelectedId = _selectedFolderId;
    AppLogger.info(
      'LibraryController.Refresh',
      'Refreshing library. previousSelectedId=$previousSelectedId, previousFolderCount=${_folders.length}',
    );
    final nextFolders = await _libraryApi.getFolders();
    _folders = _mergeRefreshedFolders(nextFolders);
    _selectedFolderId = _resolveSelectedFolderId(previousSelectedId);
    AppLogger.info(
      'LibraryController.Refresh',
      'Refresh completed. selectedFolderId=$_selectedFolderId, ${_describeFolders(_folders)}',
    );
    notifyListeners();
  }

  Future<AddLibraryFolderResultDto> addFolder(String path) async {
    AppLogger.info('LibraryController.AddFolder', 'Adding folder path=$path');
    final result = await _libraryApi.addFolder(path);
    AppLogger.info(
      'LibraryController.AddFolder',
      'Add folder result: status=${result.status}, reason=${result.reasonCode}, folderId=${result.folder?.folderId}, coverUrl=${result.folder?.coverUrl}, posterUrl=${result.folder?.metadataSummary?.posterUrl}',
    );
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

  Future<void> toggleFavorite(String folderId, bool isFavorite) async {
    await _libraryApi.setFavorite(folderId, isFavorite);
    _patchFolder(
      folderId,
      (folder) => LibraryFolderDto(
        folderId: folder.folderId,
        name: folder.name,
        videoCount: folder.videoCount,
        coverUrl: folder.coverUrl,
        playedCount: folder.playedCount,
        watchStatus: folder.watchStatus,
        isFavorite: isFavorite,
        metadataSummary: folder.metadataSummary,
      ),
    );
  }

  Future<void> setWatchStatus(String folderId, WatchStatus status) async {
    await _libraryApi.setWatchStatus(folderId, _encodeWatchStatus(status));
    _patchFolder(
      folderId,
      (folder) => LibraryFolderDto(
        folderId: folder.folderId,
        name: folder.name,
        videoCount: folder.videoCount,
        coverUrl: folder.coverUrl,
        playedCount: folder.playedCount,
        watchStatus: status,
        isFavorite: folder.isFavorite,
        metadataSummary: folder.metadataSummary,
      ),
    );
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
    AppLogger.info(
      'LibraryController.MetadataUpdate',
      'Received metadata update for folderId=${update.folderId}, state=${update.state.name}, hasMetadata=${update.hasMetadata}, coverUrl=${update.coverUrl}, posterUrl=${update.posterUrl}',
    );
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
            videoCount: folder.videoCount,
            coverUrl: update.coverUrl,
            playedCount: folder.playedCount,
            watchStatus: folder.watchStatus,
            isFavorite: folder.isFavorite,
            metadataSummary: nextMetadata,
          );
        })
        .toList(growable: false);

    if (!changed) {
      return;
    }

    _folders = nextFolders;
    AppLogger.info(
      'LibraryController.MetadataUpdate',
      'Applied metadata update. ${_describeFolders(_folders, focusFolderId: update.folderId)}',
    );
    notifyListeners();
  }

  void applyFolderAdded(LibraryFolderDto folder) {
    AppLogger.info(
      'LibraryController.FolderMutation',
      'Applying folder added. folderId=${folder.folderId}, name=${folder.name}',
    );
    final index = _folders.indexWhere(
      (item) => item.folderId == folder.folderId,
    );
    if (index >= 0) {
      final nextFolders = _folders.toList(growable: false);
      nextFolders[index] = _mergeFolder(_folders[index], folder);
      _folders = nextFolders;
      notifyListeners();
      return;
    }

    _folders = [..._folders, folder];
    notifyListeners();
  }

  void applyFolderUpdated(LibraryFolderDto folder) {
    AppLogger.info(
      'LibraryController.FolderMutation',
      'Applying folder updated. folderId=${folder.folderId}, name=${folder.name}, isFavorite=${folder.isFavorite}, watchStatus=${folder.watchStatus.name}',
    );
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
    notifyListeners();
  }

  void _patchFolder(
    String folderId,
    LibraryFolderDto Function(LibraryFolderDto folder) update,
  ) {
    AppLogger.info(
      'LibraryController.FolderMutation',
      'Patching local folder state. folderId=$folderId',
    );
    final index = _folders.indexWhere((item) => item.folderId == folderId);
    if (index < 0) {
      return;
    }

    final nextFolders = _folders.toList(growable: false);
    nextFolders[index] = update(nextFolders[index]);
    _folders = nextFolders;
    notifyListeners();
  }

  void applyFolderRemoved(String folderId) {
    AppLogger.info(
      'LibraryController.FolderMutation',
      'Applying folder removed. folderId=$folderId',
    );
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
    AppLogger.info(
      'LibraryController.FolderMutation',
      'Applying folder reordered. folderId=$folderId, position=$position, hasSnapshot=${folder != null}',
    );
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
    notifyListeners();
  }

  String? _resolveSelectedFolderId(String? candidateFolderId) {
    if (_folders.isEmpty) {
      return null;
    }

    if (candidateFolderId != null &&
        _folders.any((folder) => folder.folderId == candidateFolderId)) {
      return candidateFolderId;
    }

    return _folders.first.folderId;
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
    final coverUrl = _preferNonEmpty(next.coverUrl, previous.coverUrl);

    if (coverUrl == next.coverUrl && nextMetadata == next.metadataSummary) {
      return next;
    }

    return LibraryFolderDto(
      folderId: next.folderId,
      name: next.name,
      videoCount: next.videoCount,
      coverUrl: coverUrl,
      playedCount: next.playedCount,
      watchStatus: next.watchStatus,
      isFavorite: next.isFavorite,
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

  String _describeFolders(
    List<LibraryFolderDto> folders, {
    String? focusFolderId,
  }) {
    final withArtwork = folders
        .where(
          (folder) =>
              (folder.coverUrl?.isNotEmpty ?? false) ||
              (folder.metadataSummary?.posterUrl?.isNotEmpty ?? false),
        )
        .length;
    final withoutArtwork = folders.length - withArtwork;
    LibraryFolderDto? focusFolder;
    if (focusFolderId != null) {
      for (final folder in folders) {
        if (folder.folderId == focusFolderId) {
          focusFolder = folder;
          break;
        }
      }
    }

    final focusDescription = focusFolder == null
        ? ''
        : ', focusFolder={id=${focusFolder.folderId}, name=${focusFolder.name}, coverUrl=${focusFolder.coverUrl}, posterUrl=${focusFolder.metadataSummary?.posterUrl}}';

    return 'folderCount=${folders.length}, withArtwork=$withArtwork, withoutArtwork=$withoutArtwork$focusDescription';
  }
}
