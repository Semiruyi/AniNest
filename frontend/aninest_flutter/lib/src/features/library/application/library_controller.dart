import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
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
    _folders = await _libraryApi.getFolders();
    _selectedFolderId = _resolveSelectedFolderId(previousSelectedId);
    notifyListeners();
  }

  Future<AddLibraryFolderResultDto> addFolder(String path) async {
    final result = await _libraryApi.addFolder(path);
    if (result.isAdded) {
      await refresh();
      selectFolder(result.folder?.folderId);
    }
    return result;
  }

  Future<void> toggleFavorite(String folderId, bool isFavorite) async {
    await _libraryApi.setFavorite(folderId, isFavorite);
    await refresh();
  }

  Future<void> setWatchStatus(String folderId, WatchStatus status) async {
    await _libraryApi.setWatchStatus(folderId, _encodeWatchStatus(status));
    await refresh();
  }

  Future<void> moveToFront(String folderId) async {
    await _libraryApi.moveToFront(folderId);
    await refresh();
    selectFolder(folderId);
  }

  Future<void> deleteFolder(String folderId) async {
    await _libraryApi.deleteFolder(folderId);
    if (_selectedFolderId == folderId) {
      _selectedFolderId = null;
    }
    await refresh();
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
}
