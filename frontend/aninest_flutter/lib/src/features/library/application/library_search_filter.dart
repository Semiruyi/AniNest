import 'package:aninest_flutter/src/models/library_models.dart';

class LibrarySearchFilter {
  const LibrarySearchFilter();

  List<LibraryFolderDto> apply(String query, List<LibraryFolderDto> folders) {
    final normalizedQuery = query.trim().toLowerCase();
    if (normalizedQuery.isEmpty) {
      return folders;
    }

    return folders
        .where((folder) => _matches(folder, normalizedQuery))
        .toList(growable: false);
  }

  bool _matches(LibraryFolderDto folder, String normalizedQuery) {
    return _searchableFields(
      folder,
    ).any((field) => field.toLowerCase().contains(normalizedQuery));
  }

  Iterable<String> _searchableFields(LibraryFolderDto folder) sync* {
    yield folder.name;
    yield folder.path;

    final metadata = folder.metadataSummary;
    if (metadata == null) {
      return;
    }

    final matchedTitle = metadata.matchedTitle;
    if (matchedTitle != null && matchedTitle.isNotEmpty) {
      yield matchedTitle;
    }

    final originalTitle = metadata.originalTitle;
    if (originalTitle != null && originalTitle.isNotEmpty) {
      yield originalTitle;
    }
  }
}
