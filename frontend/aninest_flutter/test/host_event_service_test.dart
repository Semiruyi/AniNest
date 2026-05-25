import 'dart:async';
import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/services/host_event_service.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  test(
    'HostEventService emits metadata folder updated envelopes from SSE',
    () async {
      final payload = utf8.encode(
        'event: metadata.folder_updated\n'
        'data: {"type":"metadata.folder_updated","timestampUtc":"2026-05-24T12:34:56Z","sequence":7,"payload":{"folderId":"bocchi-the-rock","state":"Ready","failureKind":"None","hasMetadata":true,"title":"Bocchi the Rock!","posterUrl":"/api/resources/library-poster/bocchi-the-rock","coverUrl":"/api/resources/library-cover/bocchi-the-rock","updatedAtUtc":"2026-05-24T12:34:56Z"}}\n\n',
      );
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _StreamingHttpClient(payload),
      );
      final service = HostEventService(client);

      final envelopeFuture = service.events.first;
      service.start();

      final envelope = await envelopeFuture.timeout(const Duration(seconds: 2));

      expect(envelope.type, 'metadata.folder_updated');
      expect(envelope.sequence, 7);
      final metadata = MetadataFolderUpdatedEventDto.fromJson(
        (envelope.payload as Map).cast<String, dynamic>(),
      );
      expect(metadata.folderId, 'bocchi-the-rock');
      expect(metadata.hasMetadata, isTrue);
      expect(metadata.matchedTitle, 'Bocchi the Rock!');
      expect(metadata.coverUrl, '/api/resources/library-cover/bocchi-the-rock');

      await service.dispose();
      client.close();
    },
  );

  test(
    'LibraryController patches metadata title and cover from host event',
    () async {
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _JsonHttpClient({
          '/api/library/folders': {
            'items': [
              {
                'folderId': 'bocchi-the-rock',
                'name': 'Bocchi',
                'videoCount': 12,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'metadataSummary': null,
              },
            ],
          },
        }),
      );
      final controller = LibraryController(LibraryApi(client));

      await controller.refresh();
      controller.applyMetadataFolderUpdate(
        MetadataFolderUpdatedEventDto.fromJson({
          'folderId': 'bocchi-the-rock',
          'state': 'Ready',
          'failureKind': 'None',
          'hasMetadata': true,
          'title': 'Bocchi the Rock!',
          'posterUrl': '/api/resources/library-poster/bocchi-the-rock',
          'coverUrl': '/api/resources/library-cover/bocchi-the-rock',
          'updatedAtUtc': '2026-05-24T12:34:56Z',
        }),
      );

      final folder = controller.folders.single;
      expect(folder.coverUrl, '/api/resources/library-cover/bocchi-the-rock');
      expect(folder.metadataSummary?.matchedTitle, 'Bocchi the Rock!');
      expect(
        folder.metadataSummary?.posterUrl,
        '/api/resources/library-poster/bocchi-the-rock',
      );

      client.close();
    },
  );

  test(
    'LibraryController preserves existing artwork when refresh snapshot omits it',
    () async {
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _SequenceJsonHttpClient({
          '/api/library/folders': [
            {
              'items': [
                {
                  'folderId': 'bocchi-the-rock',
                  'name': 'Bocchi',
                  'videoCount': 12,
                  'coverUrl': '/api/resources/library-cover/bocchi-the-rock',
                  'playedCount': 0,
                  'watchStatus': 'planned',
                  'isFavorite': false,
                  'metadataSummary': {
                    'matchedTitle': 'Bocchi the Rock!',
                    'originalTitle': 'Bocchi the Rock!',
                    'posterUrl':
                        '/api/resources/library-poster/bocchi-the-rock',
                    'state': 'Ready',
                    'hasMetadata': true,
                  },
                },
              ],
            },
            {
              'items': [
                {
                  'folderId': 'bocchi-the-rock',
                  'name': 'Bocchi',
                  'videoCount': 12,
                  'coverUrl': null,
                  'playedCount': 0,
                  'watchStatus': 'planned',
                  'isFavorite': false,
                  'metadataSummary': null,
                },
                {
                  'folderId': 'new-folder',
                  'name': 'New Folder',
                  'videoCount': 6,
                  'coverUrl': null,
                  'playedCount': 0,
                  'watchStatus': 'planned',
                  'isFavorite': false,
                  'metadataSummary': null,
                },
              ],
            },
          ],
        }),
      );
      final controller = LibraryController(LibraryApi(client));

      await controller.refresh();
      await controller.refresh();

      final existing = controller.folders.firstWhere(
        (folder) => folder.folderId == 'bocchi-the-rock',
      );
      final added = controller.folders.firstWhere(
        (folder) => folder.folderId == 'new-folder',
      );
      expect(existing.coverUrl, '/api/resources/library-cover/bocchi-the-rock');
      expect(
        existing.metadataSummary?.posterUrl,
        '/api/resources/library-poster/bocchi-the-rock',
      );
      expect(added.coverUrl, isNull);
      expect(added.metadataSummary, isNull);

      client.close();
    },
  );

  test(
    'LibraryController metadata update can explicitly clear artwork',
    () async {
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _JsonHttpClient({
          '/api/library/folders': {
            'items': [
              {
                'folderId': 'bocchi-the-rock',
                'name': 'Bocchi',
                'videoCount': 12,
                'coverUrl': '/api/resources/library-cover/bocchi-the-rock',
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'metadataSummary': {
                  'matchedTitle': 'Bocchi the Rock!',
                  'originalTitle': 'Bocchi the Rock!',
                  'posterUrl': '/api/resources/library-poster/bocchi-the-rock',
                  'state': 'Ready',
                  'hasMetadata': true,
                },
              },
            ],
          },
        }),
      );
      final controller = LibraryController(LibraryApi(client));

      await controller.refresh();
      controller.applyMetadataFolderUpdate(
        MetadataFolderUpdatedEventDto.fromJson({
          'folderId': 'bocchi-the-rock',
          'state': 'NeedsMetadata',
          'failureKind': 'None',
          'hasMetadata': false,
          'title': null,
          'posterUrl': null,
          'coverUrl': null,
          'updatedAtUtc': '2026-05-24T12:34:56Z',
        }),
      );

      final folder = controller.folders.single;
      expect(folder.coverUrl, isNull);
      expect(folder.metadataSummary, isNull);

      client.close();
    },
  );

  test(
    'LibraryController applies add update remove and reorder events',
    () async {
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _JsonHttpClient({
          '/api/library/folders': {
            'items': [
              {
                'folderId': 'first',
                'name': 'First',
                'videoCount': 12,
                'coverUrl': '/api/resources/library-cover/first',
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'metadataSummary': null,
              },
              {
                'folderId': 'second',
                'name': 'Second',
                'videoCount': 8,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'watching',
                'isFavorite': false,
                'metadataSummary': null,
              },
            ],
          },
        }),
      );
      final controller = LibraryController(LibraryApi(client));
      await controller.refresh();

      controller.applyFolderAdded(
        LibraryFolderDto.fromJson({
          'folderId': 'third',
          'name': 'Third',
          'videoCount': 6,
          'coverUrl': null,
          'playedCount': 0,
          'watchStatus': 'planned',
          'isFavorite': false,
          'metadataSummary': null,
        }),
      );
      controller.applyFolderUpdated(
        LibraryFolderDto.fromJson({
          'folderId': 'second',
          'name': 'Second',
          'videoCount': 8,
          'coverUrl': null,
          'playedCount': 0,
          'watchStatus': 'completed',
          'isFavorite': true,
          'metadataSummary': null,
        }),
      );
      controller.applyFolderReordered('third', 0);
      controller.applyFolderRemoved('first');

      expect(controller.folders.map((folder) => folder.folderId).toList(), [
        'third',
        'second',
      ]);
      final second = controller.folders.last;
      expect(second.isFavorite, isTrue);
      expect(second.watchStatus, WatchStatus.completed);

      client.close();
    },
  );

  test(
    'LibraryController applies local library mutations without refresh',
    () async {
      final client = AniNestHttpClient(
        baseUrl: 'http://localhost:5275',
        httpClient: _JsonHttpClient({
          '/api/library/folders': {
            'items': [
              {
                'folderId': 'first',
                'name': 'First',
                'videoCount': 12,
                'coverUrl': '/api/resources/library-cover/first',
                'playedCount': 0,
                'watchStatus': 'planned',
                'isFavorite': false,
                'metadataSummary': null,
              },
              {
                'folderId': 'second',
                'name': 'Second',
                'videoCount': 8,
                'coverUrl': null,
                'playedCount': 0,
                'watchStatus': 'watching',
                'isFavorite': false,
                'metadataSummary': null,
              },
            ],
          },
          '/api/library/folders/second:favorite': {},
          '/api/library/folders/second:watch-status': {},
          '/api/library/folders/second:move-to-front': {},
          '/api/library/folders/first': {},
        }),
      );
      final controller = LibraryController(LibraryApi(client));

      await controller.refresh();
      controller.selectFolder('second');
      await controller.toggleFavorite('second', true);
      await controller.setWatchStatus('second', WatchStatus.completed);
      await controller.moveToFront('second');
      await controller.deleteFolder('first');

      expect(controller.folders.map((folder) => folder.folderId).toList(), [
        'second',
      ]);
      expect(controller.selectedFolderId, 'second');
      final second = controller.folders.single;
      expect(second.isFavorite, isTrue);
      expect(second.watchStatus, WatchStatus.completed);

      client.close();
    },
  );
}

class _StreamingHttpClient extends http.BaseClient {
  _StreamingHttpClient(this._payload);

  final List<int> _payload;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    return http.StreamedResponse(
      Stream<List<int>>.fromIterable([_payload]),
      200,
      headers: const {'content-type': 'text/event-stream'},
    );
  }
}

class _JsonHttpClient extends http.BaseClient {
  _JsonHttpClient(this._responses);

  final Map<String, Object?> _responses;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    final body = _responses[path];
    if (body == null) {
      return http.StreamedResponse(
        Stream<List<int>>.fromIterable([utf8.encode('{}')]),
        404,
        headers: const {'content-type': 'application/json'},
      );
    }

    return http.StreamedResponse(
      Stream<List<int>>.fromIterable([utf8.encode(jsonEncode(body))]),
      200,
      headers: const {'content-type': 'application/json'},
    );
  }
}

class _SequenceJsonHttpClient extends http.BaseClient {
  _SequenceJsonHttpClient(this._responses);

  final Map<String, List<Object?>> _responses;
  final Map<String, int> _indices = <String, int>{};

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    final responses = _responses[path];
    if (responses == null || responses.isEmpty) {
      return http.StreamedResponse(
        Stream<List<int>>.fromIterable([utf8.encode('{}')]),
        404,
        headers: const {'content-type': 'application/json'},
      );
    }

    final index = _indices[path] ?? 0;
    final nextIndex = index < responses.length - 1 ? index + 1 : index;
    _indices[path] = nextIndex;
    final body = responses[index];

    return http.StreamedResponse(
      Stream<List<int>>.fromIterable([utf8.encode(jsonEncode(body))]),
      200,
      headers: const {'content-type': 'application/json'},
    );
  }
}
