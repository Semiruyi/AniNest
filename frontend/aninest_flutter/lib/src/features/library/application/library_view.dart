import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/enums.dart';

enum LibraryView {
  allMedia,
  recentlyAdded,
  continueWatching,
  favorites,
  unknown,
  watching,
  completed,
  onHold,
  dropped,
  planned,
}

class LibraryViewFilter {
  const LibraryViewFilter();

  List<LibraryFolderDto> apply(
    LibraryView view,
    List<LibraryFolderDto> folders,
  ) {
    return switch (view) {
      LibraryView.allMedia => folders,
      LibraryView.recentlyAdded => _recentlyAdded(folders),
      LibraryView.continueWatching =>
        folders
            .where(
              (folder) =>
                  folder.videoCount > 0 &&
                  folder.playedCount > 0 &&
                  folder.playedCount < folder.videoCount,
            )
            .toList(growable: false),
      LibraryView.favorites =>
        folders.where((folder) => folder.isFavorite).toList(growable: false),
      LibraryView.unknown => _byWatchStatus(folders, WatchStatus.unknown),
      LibraryView.watching => _byWatchStatus(folders, WatchStatus.watching),
      LibraryView.completed => _byWatchStatus(folders, WatchStatus.completed),
      LibraryView.onHold => _byWatchStatus(folders, WatchStatus.onHold),
      LibraryView.dropped => _byWatchStatus(folders, WatchStatus.dropped),
      LibraryView.planned => _byWatchStatus(folders, WatchStatus.planned),
    };
  }

  List<LibraryFolderDto> _byWatchStatus(
    List<LibraryFolderDto> folders,
    WatchStatus status,
  ) {
    return folders
        .where((folder) => folder.watchStatus == status)
        .toList(growable: false);
  }

  List<LibraryFolderDto> _recentlyAdded(List<LibraryFolderDto> folders) {
    final indexed = <_IndexedFolder>[
      for (var index = 0; index < folders.length; index++)
        _IndexedFolder(index, folders[index]),
    ];

    indexed.sort((a, b) {
      final aAddedAt = a.folder.addedAtUtc;
      final bAddedAt = b.folder.addedAtUtc;
      if (aAddedAt != null && bAddedAt != null) {
        final byAddedAt = bAddedAt.compareTo(aAddedAt);
        if (byAddedAt != 0) {
          return byAddedAt;
        }
      } else if (aAddedAt != null) {
        return -1;
      } else if (bAddedAt != null) {
        return 1;
      }

      return a.index.compareTo(b.index);
    });

    return indexed.map((item) => item.folder).toList(growable: false);
  }
}

class _IndexedFolder {
  const _IndexedFolder(this.index, this.folder);

  final int index;
  final LibraryFolderDto folder;
}
