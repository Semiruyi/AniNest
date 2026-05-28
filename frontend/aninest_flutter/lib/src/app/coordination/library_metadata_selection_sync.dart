import 'dart:async';

import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';

class LibraryMetadataSelectionSync {
  LibraryMetadataSelectionSync({
    required LibraryController library,
    required MetadataController metadata,
  }) : _library = library,
       _metadata = metadata {
    _library.addListener(_handleLibrarySelectionChanged);
  }

  final LibraryController _library;
  final MetadataController _metadata;

  String? _lastMetadataFolderId;
  int _selectionRefreshSuspensionCount = 0;

  Future<void> refreshForCurrentSelection({bool force = false}) async {
    final folderId = _library.selectedFolderId;
    if (!force && folderId == _lastMetadataFolderId) {
      return;
    }

    _lastMetadataFolderId = folderId;
    await _metadata.refresh(folderId);
  }

  Future<T> runWithSelectionRefreshSuspended<T>(
    Future<T> Function() action,
  ) async {
    _selectionRefreshSuspensionCount++;
    try {
      return await action();
    } finally {
      _selectionRefreshSuspensionCount--;
    }
  }

  void dispose() {
    _library.removeListener(_handleLibrarySelectionChanged);
  }

  void _handleLibrarySelectionChanged() {
    if (_selectionRefreshSuspensionCount > 0) {
      return;
    }

    final currentFolderId = _library.selectedFolderId;
    if (currentFolderId == _lastMetadataFolderId) {
      return;
    }

    unawaited(refreshForCurrentSelection());
  }
}
