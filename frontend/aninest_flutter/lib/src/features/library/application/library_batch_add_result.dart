import 'package:aninest_flutter/src/models/library_models.dart';

class LibraryBatchAddResult {
  const LibraryBatchAddResult({
    required this.rootPath,
    required this.addedFolders,
  });

  final String rootPath;
  final List<LibraryFolderDto> addedFolders;

  int get addedCount => addedFolders.length;
  bool get hasAddedFolders => addedFolders.isNotEmpty;
}
