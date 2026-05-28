import 'package:aninest_flutter/src/models/library_models.dart';

typedef ReadSelectedFolderId = String? Function();
typedef ReadLibraryFolders = List<LibraryFolderDto> Function();
typedef ReadSelectedItemId = String? Function();

class AppSelectionResolver {
  AppSelectionResolver({
    required ReadSelectedFolderId readLibrarySelectedFolderId,
    required ReadSelectedFolderId readPlayerSelectedFolderId,
    required ReadLibraryFolders readLibraryFolders,
    required ReadSelectedItemId readSelectedItemId,
  }) : _readLibrarySelectedFolderId = readLibrarySelectedFolderId,
       _readPlayerSelectedFolderId = readPlayerSelectedFolderId,
       _readLibraryFolders = readLibraryFolders,
       _readSelectedItemId = readSelectedItemId;

  final ReadSelectedFolderId _readLibrarySelectedFolderId;
  final ReadSelectedFolderId _readPlayerSelectedFolderId;
  final ReadLibraryFolders _readLibraryFolders;
  final ReadSelectedItemId _readSelectedItemId;

  String? resolveSelectedFolderId() {
    final folders = _readLibraryFolders();
    return _readLibrarySelectedFolderId() ??
        _readPlayerSelectedFolderId() ??
        (folders.isNotEmpty ? folders.first.folderId : null);
  }

  String? resolveSelectedItemId() {
    return _readSelectedItemId();
  }
}
