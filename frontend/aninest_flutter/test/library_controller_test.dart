import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
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
