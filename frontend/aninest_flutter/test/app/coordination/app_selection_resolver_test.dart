import 'package:aninest_flutter/src/app/coordination/app_selection_resolver.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('prefers library selection over player and folder fallback', () {
    String? librarySelectedFolderId = 'library-folder';
    String? playerSelectedFolderId = 'player-folder';
    String? selectedItemId = 'item-01';
    var folders = <LibraryFolderDto>[_folder('folder-01')];

    final resolver = AppSelectionResolver(
      readLibrarySelectedFolderId: () => librarySelectedFolderId,
      readPlayerSelectedFolderId: () => playerSelectedFolderId,
      readLibraryFolders: () => folders,
      readSelectedItemId: () => selectedItemId,
    );

    expect(resolver.resolveSelectedFolderId(), 'library-folder');
    expect(resolver.resolveSelectedItemId(), 'item-01');
  });

  test('falls back to player selection and then first library folder', () {
    String? librarySelectedFolderId;
    String? playerSelectedFolderId = 'player-folder';
    final folders = <LibraryFolderDto>[
      _folder('folder-01'),
      _folder('folder-02'),
    ];

    final resolver = AppSelectionResolver(
      readLibrarySelectedFolderId: () => librarySelectedFolderId,
      readPlayerSelectedFolderId: () => playerSelectedFolderId,
      readLibraryFolders: () => folders,
      readSelectedItemId: () => null,
    );

    expect(resolver.resolveSelectedFolderId(), 'player-folder');

    playerSelectedFolderId = null;
    expect(resolver.resolveSelectedFolderId(), 'folder-01');
  });

  test('returns null when no folder sources are available', () {
    final resolver = AppSelectionResolver(
      readLibrarySelectedFolderId: () => null,
      readPlayerSelectedFolderId: () => null,
      readLibraryFolders: () => const <LibraryFolderDto>[],
      readSelectedItemId: () => null,
    );

    expect(resolver.resolveSelectedFolderId(), isNull);
    expect(resolver.resolveSelectedItemId(), isNull);
  });
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
