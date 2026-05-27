import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_mapper.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_option.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_folder_card.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/inspector_widgets/details/library_inspector_title_block.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:media_kit/media_kit.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

void main() {
  test('AniNestHttpClient keeps a normalized base url', () {
    final client = AniNestHttpClient(baseUrl: 'http://localhost:5275/');

    expect(client.baseUrl, 'http://localhost:5275');

    client.updateBaseUrl('http://127.0.0.1:5275/');

    expect(client.baseUrl, 'http://127.0.0.1:5275');
  });

  test('AniNestHttpClient validates backend base urls', () {
    expect(
      AniNestHttpClient.isValidBaseUrl('http://192.168.31.10:5275'),
      isTrue,
    );
    expect(
      AniNestHttpClient.isValidBaseUrl('https://aninest.local/api'),
      isTrue,
    );
    expect(AniNestHttpClient.isValidBaseUrl('192.168.31.10:5275'), isFalse);
    expect(AniNestHttpClient.isValidBaseUrl('ftp://aninest.local'), isFalse);
  });

  test('SessionApi returns null when backend has no active session', () async {
    final client = AniNestHttpClient(
      baseUrl: 'http://localhost:5275',
      httpClient: _FakeHttpClient((request) async {
        return http.Response(
          'null',
          200,
          headers: <String, String>{'content-type': 'application/json'},
        );
      }),
    );

    final api = SessionApi(client);

    expect(await api.getCurrent(), isNull);
  });

  test('SessionApi reports progress and completion', () async {
    final requests = <http.Request>[];
    final client = AniNestHttpClient(
      baseUrl: 'http://localhost:5275',
      httpClient: _FakeHttpClient((request) async {
        requests.add(request as http.Request);
        return http.Response('', 202);
      }),
    );

    final api = SessionApi(client);

    await api.reportProgress(
      itemId: 'ep-02',
      positionMs: 120000,
      durationMs: 1440000,
      rate: 1.25,
      volume: 64,
      isPaused: false,
    );
    await api.complete('ep-02');

    expect(requests[0].url.path, '/api/session/progress');
    expect(jsonDecode(requests[0].body), containsPair('positionMs', 120000));
    expect(requests[1].url.path, '/api/session/complete');
    expect(jsonDecode(requests[1].body), containsPair('itemId', 'ep-02'));
  });

  test('PlayerSubtitleTrackMapper maps selectable subtitle tracks', () {
    final options = PlayerSubtitleTrackMapper.fromMediaKitTracks(
      <SubtitleTrack>[
        SubtitleTrack.auto(),
        SubtitleTrack.no(),
        const SubtitleTrack('1', 'Simplified Chinese', 'zh-Hans', codec: 'ass'),
        const SubtitleTrack('1', 'Duplicate', 'zh-Hans'),
        SubtitleTrack.uri('https://example.com/subs.vtt', title: 'External'),
      ],
    );

    expect(options, hasLength(4));
    expect(options[0].kind, PlayerSubtitleTrackKind.automatic);
    expect(options[1].kind, PlayerSubtitleTrackKind.off);
    expect(options[2].kind, PlayerSubtitleTrackKind.embedded);
    expect(options[2].index, 1);
    expect(options[3].kind, PlayerSubtitleTrackKind.external);
    expect(options[3].index, 2);
  });

  testWidgets('LibraryFolderCard shows folder name instead of metadata title', (
    tester,
  ) async {
    const folder = LibraryFolderDto(
      folderId: 'bocchi-the-rock',
      name: 'Bocchi The Rock Folder',
      videoCount: 12,
      coverUrl: null,
      playedCount: 0,
      watchStatus: WatchStatus.planned,
      isFavorite: false,
      addedAtUtc: null,
      metadataSummary: LibraryMetadataSummaryDto(
        matchedTitle: 'Bocchi the Rock!',
        originalTitle: 'Bocchi the Rock!',
        posterUrl: null,
        state: 'Ready',
        hasMetadata: true,
      ),
    );

    await tester.pumpWidget(
      ShadcnApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Material(
          child: LibraryFolderCard(
            folder: folder,
            imageUrl: null,
            isSelected: false,
            onPressed: () {},
            onContextMenuRequested: () {},
            onOpen: () {},
            onToggleFavorite: (_) {},
            onSetWatchStatus: (_) {},
            onMoveToFront: () {},
            onDelete: () {},
          ),
        ),
      ),
    );

    expect(find.text('Bocchi The Rock Folder'), findsWidgets);
    expect(find.text('Bocchi the Rock!'), findsNothing);
  });

  testWidgets('LibraryInspectorTitleBlock shows folder name as primary title', (
    tester,
  ) async {
    const folder = LibraryFolderDto(
      folderId: 'bocchi-the-rock',
      name: 'Bocchi The Rock Folder',
      videoCount: 12,
      coverUrl: null,
      playedCount: 0,
      watchStatus: WatchStatus.planned,
      isFavorite: false,
      addedAtUtc: null,
      metadataSummary: LibraryMetadataSummaryDto(
        matchedTitle: 'Bocchi the Rock!',
        originalTitle: 'Bocchi the Rock!',
        posterUrl: null,
        state: 'Ready',
        hasMetadata: true,
      ),
    );

    await tester.pumpWidget(
      ShadcnApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Material(child: LibraryInspectorTitleBlock(folder: folder)),
      ),
    );

    expect(find.text('Bocchi The Rock Folder'), findsOneWidget);
    expect(find.text('Bocchi the Rock!'), findsOneWidget);
  });
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
