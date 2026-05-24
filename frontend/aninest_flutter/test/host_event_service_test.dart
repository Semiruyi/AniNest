import 'dart:async';
import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
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
        'data: {"type":"metadata.folder_updated","timestampUtc":"2026-05-24T12:34:56Z","payload":{"folderId":"bocchi-the-rock","state":"Ready","failureKind":"None","hasMetadata":true,"title":"Bocchi the Rock!","posterUrl":"/api/resources/library-poster/bocchi-the-rock","coverUrl":"/api/resources/library-cover/bocchi-the-rock","updatedAtUtc":"2026-05-24T12:34:56Z"}}\n\n',
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
      final metadata = MetadataFolderUpdatedEventDto.fromJson(
        (envelope.payload as Map).cast<String, dynamic>(),
      );
      expect(metadata.folderId, 'bocchi-the-rock');
      expect(metadata.hasMetadata, isTrue);
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
      expect(folder.metadataSummary?.title, 'Bocchi the Rock!');
      expect(
        folder.metadataSummary?.posterUrl,
        '/api/resources/library-poster/bocchi-the-rock',
      );

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
