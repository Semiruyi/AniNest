import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/library/application/library_search_filter.dart';
import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  test('LibraryViewFilter filters folders by watch status', () {
    const folders = <LibraryFolderDto>[
      LibraryFolderDto(
        folderId: 'watching',
        name: 'Watching',
        path: '/anime/watching',
        videoCount: 12,
        coverUrl: null,
        playedCount: 4,
        watchStatus: WatchStatus.watching,
        isFavorite: false,
        addedAtUtc: null,
        metadataSummary: null,
      ),
      LibraryFolderDto(
        folderId: 'completed',
        name: 'Completed',
        path: '/anime/completed',
        videoCount: 12,
        coverUrl: null,
        playedCount: 12,
        watchStatus: WatchStatus.completed,
        isFavorite: false,
        addedAtUtc: null,
        metadataSummary: null,
      ),
    ];

    final filter = LibraryViewFilter();

    expect(filter.apply(LibraryView.watching, folders), hasLength(1));
    expect(
      filter.apply(LibraryView.watching, folders).first.folderId,
      'watching',
    );
    expect(filter.apply(LibraryView.completed, folders), hasLength(1));
    expect(
      filter.apply(LibraryView.completed, folders).first.folderId,
      'completed',
    );
  });

  test('LibrarySearchFilter matches folder name metadata and path', () {
    const folders = <LibraryFolderDto>[
      LibraryFolderDto(
        folderId: 'folder-01',
        name: 'Frieren',
        path: '/anime/frieren',
        videoCount: 28,
        coverUrl: null,
        playedCount: 0,
        watchStatus: WatchStatus.planned,
        isFavorite: false,
        addedAtUtc: null,
        metadataSummary: null,
      ),
      LibraryFolderDto(
        folderId: 'folder-02',
        name: 'Sousou Project',
        path: '/anime/seasonal/drama',
        videoCount: 24,
        coverUrl: null,
        playedCount: 0,
        watchStatus: WatchStatus.watching,
        isFavorite: false,
        addedAtUtc: null,
        metadataSummary: LibraryMetadataSummaryDto(
          matchedTitle: 'Bocchi the Rock!',
          originalTitle: 'ぼっち・ざ・ろっく！',
          posterUrl: null,
          state: 'Matched',
          hasMetadata: true,
        ),
      ),
    ];

    final filter = LibrarySearchFilter();

    expect(
      filter.apply('fri', folders).map((folder) => folder.folderId),
      <String>['folder-01'],
    );
    expect(
      filter.apply('bocchi', folders).map((folder) => folder.folderId),
      <String>['folder-02'],
    );
    expect(
      filter.apply('seasonal', folders).map((folder) => folder.folderId),
      <String>['folder-02'],
    );
  });

  test(
    'LibraryController applies search query after current view filtering',
    () {
      final controller = _createController();

      controller.applyFolderAdded(
        const LibraryFolderDto(
          folderId: 'favorite-match',
          name: 'Frieren',
          path: '/anime/frieren',
          videoCount: 28,
          coverUrl: null,
          playedCount: 0,
          watchStatus: WatchStatus.watching,
          isFavorite: true,
          addedAtUtc: null,
          metadataSummary: null,
        ),
      );
      controller.applyFolderAdded(
        const LibraryFolderDto(
          folderId: 'favorite-miss',
          name: 'Dungeon Meshi',
          path: '/anime/dungeon-meshi',
          videoCount: 24,
          coverUrl: null,
          playedCount: 0,
          watchStatus: WatchStatus.watching,
          isFavorite: true,
          addedAtUtc: null,
          metadataSummary: null,
        ),
      );
      controller.applyFolderAdded(
        const LibraryFolderDto(
          folderId: 'non-favorite-match',
          name: 'Frieren Specials',
          path: '/anime/frieren-specials',
          videoCount: 4,
          coverUrl: null,
          playedCount: 0,
          watchStatus: WatchStatus.planned,
          isFavorite: false,
          addedAtUtc: null,
          metadataSummary: null,
        ),
      );

      controller.selectView(LibraryView.favorites);
      controller.setSearchQuery('fri');

      expect(controller.viewFilteredFolders.map((folder) => folder.folderId), [
        'favorite-match',
        'favorite-miss',
      ]);
      expect(controller.visibleFolders.map((folder) => folder.folderId), [
        'favorite-match',
      ]);
      expect(controller.selectedFolderId, 'favorite-match');
      expect(controller.searchQuery, 'fri');
    },
  );

  test(
    'LibraryController sends numeric watch status values to backend',
    () async {
      final requests = <http.Request>[];
      final responses = <http.Response>[
        http.Response(
          jsonEncode(<String, Object?>{
            'items': <Object?>[
              <String, Object?>{
                'folderId': 'folder-01',
                'name': 'Folder 01',
                'path': '/anime/Folder 01',
                'videoCount': 12,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'watching',
                'isFavorite': false,
                'addedAtUtc': '2026-05-28T10:00:00Z',
              },
            ],
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        ),
        http.Response('', 200),
      ];

      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _FakeHttpClient((request) async {
          requests.add(request as http.Request);
          return responses.removeAt(0);
        }),
      );

      final controller = LibraryController(LibraryApi(client));
      await controller.refresh();

      await controller.setWatchStatus('folder-01', WatchStatus.completed);

      expect(
        requests[1].url.path,
        '/api/library/folders/folder-01:watch-status',
      );
      expect(
        jsonDecode(requests[1].body),
        containsPair('status', WatchStatus.completed.index),
      );
      expect(controller.folders.single.watchStatus, WatchStatus.completed);
    },
  );

  test(
    'LibraryController addFolderBatch refreshes folders and reports new ones',
    () async {
      final requests = <http.Request>[];
      final responses = <http.Response>[
        http.Response(
          jsonEncode(<String, Object?>{
            'items': <Object?>[
              <String, Object?>{
                'folderId': 'folder-01',
                'name': 'Folder 01',
                'path': '/anime/Folder 01',
                'videoCount': 12,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'watching',
                'isFavorite': false,
                'addedAtUtc': '2026-05-28T10:00:00Z',
              },
            ],
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        ),
        http.Response('', 202),
        http.Response(
          jsonEncode(<String, Object?>{
            'items': <Object?>[
              <String, Object?>{
                'folderId': 'folder-01',
                'name': 'Folder 01',
                'path': '/anime/Folder 01',
                'videoCount': 12,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'watching',
                'isFavorite': false,
                'addedAtUtc': '2026-05-28T10:00:00Z',
              },
              <String, Object?>{
                'folderId': 'season-a',
                'name': 'Season A',
                'path': '/anime/Import Root/Season A',
                'videoCount': 10,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'addedAtUtc': '2026-05-28T10:05:00Z',
              },
              <String, Object?>{
                'folderId': 'season-b',
                'name': 'Season B',
                'path': '/anime/Import Root/Season B',
                'videoCount': 8,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'addedAtUtc': '2026-05-28T10:06:00Z',
              },
            ],
          }),
          200,
          headers: <String, String>{'content-type': 'application/json'},
        ),
      ];

      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _FakeHttpClient((request) async {
          requests.add(request as http.Request);
          return responses.removeAt(0);
        }),
      );

      final controller = LibraryController(LibraryApi(client));
      await controller.refresh();

      final result = await controller.addFolderBatch('/anime/Import Root');

      expect(requests[0].url.path, '/api/library/folders');
      expect(requests[1].url.path, '/api/library/folders:batch-add');
      expect(
        jsonDecode(requests[1].body),
        containsPair('rootPath', '/anime/Import Root'),
      );
      expect(requests[2].url.path, '/api/library/folders');
      expect(result.addedCount, 2);
      expect(result.addedFolders.map((folder) => folder.folderId), <String>[
        'season-a',
        'season-b',
      ]);
      expect(controller.folders, hasLength(3));
    },
  );
}

LibraryController _createController() {
  final client = AniNestHttpClient(
    baseUrl: 'http://localhost:5275',
    httpClient: _FakeHttpClient((_) async {
      throw UnimplementedError('This test should not hit HTTP.');
    }),
  );
  return LibraryController(LibraryApi(client));
}

class _FakeHttpClient extends http.BaseClient {
  _FakeHttpClient(this._handler);

  final Future<http.Response> Function(http.BaseRequest request) _handler;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final response = await _handler(request);
    return http.StreamedResponse(
      Stream<List<int>>.fromIterable(<List<int>>[response.bodyBytes]),
      response.statusCode,
      headers: response.headers,
      reasonPhrase: response.reasonPhrase,
      request: request,
    );
  }
}
