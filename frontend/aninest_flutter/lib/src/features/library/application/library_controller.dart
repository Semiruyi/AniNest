import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter/foundation.dart';

class LibraryController extends ChangeNotifier {
  LibraryController(this._libraryApi);

  LibraryApi _libraryApi;

  List<LibraryFolderDto> _folders = const [];

  List<LibraryFolderDto> get folders => _folders;

  void rebind(LibraryApi libraryApi) {
    _libraryApi = libraryApi;
  }

  Future<void> refresh() async {
    _folders = await _libraryApi.getFolders();
    notifyListeners();
  }

  Future<AddLibraryFolderResultDto> addFolder(String path) async {
    final result = await _libraryApi.addFolder(path);
    if (result.isAdded) {
      await refresh();
    }
    return result;
  }

  void clear() {
    if (_folders.isEmpty) {
      return;
    }

    _folders = const [];
    notifyListeners();
  }
}
