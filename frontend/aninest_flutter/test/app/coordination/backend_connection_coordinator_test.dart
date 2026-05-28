import 'dart:convert';

import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/app/coordination/backend_connection_coordinator.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

void main() {
  test(
    'hydrateBaseUrl prefers launch override once and normalizes it',
    () async {
      final preferences = _FakeAppPreferences()
        ..storedBaseUrl = 'http://saved-host:5275/';
      final client = AniNestHttpClient(baseUrl: 'http://localhost:5275');
      final reloadCalls = <bool>[];
      final coordinator = BackendConnectionCoordinator(
        launchBaseUrl: ' http://launch-host:5275/ ',
        appPreferences: preferences,
        client: client,
        reloadFromBackend: ({bool restartHostEvents = false}) async {
          reloadCalls.add(restartHostEvents);
          return null;
        },
      );

      await coordinator.hydrateBaseUrl();
      await coordinator.hydrateBaseUrl();

      expect(client.baseUrl, 'http://launch-host:5275');
      expect(preferences.loadBaseUrlCallCount, 0);
      expect(reloadCalls, isEmpty);
    },
  );

  test(
    'hydrateBaseUrl falls back to saved preference when launch override is absent',
    () async {
      final preferences = _FakeAppPreferences()
        ..storedBaseUrl = 'http://saved-host:5275/';
      final client = AniNestHttpClient(baseUrl: 'http://localhost:5275');
      final coordinator = BackendConnectionCoordinator(
        appPreferences: preferences,
        client: client,
        reloadFromBackend: ({bool restartHostEvents = false}) async => null,
      );

      await coordinator.hydrateBaseUrl();

      expect(client.baseUrl, 'http://saved-host:5275');
      expect(preferences.loadBaseUrlCallCount, 1);
    },
  );

  test('testBaseUrl returns validation error for invalid url', () async {
    final coordinator = BackendConnectionCoordinator(
      appPreferences: _FakeAppPreferences(),
      client: AniNestHttpClient(baseUrl: 'http://localhost:5275'),
      reloadFromBackend: ({bool restartHostEvents = false}) async => null,
    );

    final result = await coordinator.testBaseUrl('localhost:5275');

    expect(result, 'Please enter a full http:// or https:// backend address.');
  });

  test(
    'testBaseUrl probes normalized backend and returns null on success',
    () async {
      final requestedBaseUrls = <String>[];
      final coordinator = BackendConnectionCoordinator(
        appPreferences: _FakeAppPreferences(),
        client: AniNestHttpClient(baseUrl: 'http://localhost:5275'),
        reloadFromBackend: ({bool restartHostEvents = false}) async => null,
        createProbeClient: (baseUrl) {
          requestedBaseUrls.add(baseUrl);
          return AniNestHttpClient(
            baseUrl: baseUrl,
            httpClient: _FakeHttpClient((request) async {
              expect(request.url.toString(), '$baseUrl/api/settings');
              return http.Response(
                jsonEncode(<String, Object?>{
                  'player': <String, Object?>{
                    'preferredRate': 1.0,
                    'preferredVolume': 80,
                    'resumePlayback': true,
                  },
                  'metadata': <String, Object?>{
                    'autoScrapeMetadata': false,
                    'bangumiAccessToken': null,
                  },
                  'thumbnails': <String, Object?>{
                    'expiryDays': 30,
                    'generateOnImport': false,
                  },
                }),
                200,
                headers: <String, String>{'content-type': 'application/json'},
              );
            }),
          );
        },
      );

      final result = await coordinator.testBaseUrl('http://127.0.0.1:5275/');

      expect(result, isNull);
      expect(requestedBaseUrls, <String>['http://127.0.0.1:5275']);
    },
  );

  test(
    'updateBaseUrl saves and reloads when backend switch succeeds',
    () async {
      final preferences = _FakeAppPreferences();
      final client = AniNestHttpClient(baseUrl: 'http://localhost:5275');
      final reloadCalls = <bool>[];
      final coordinator = BackendConnectionCoordinator(
        appPreferences: preferences,
        client: client,
        reloadFromBackend: ({bool restartHostEvents = false}) async {
          reloadCalls.add(restartHostEvents);
          return null;
        },
      );

      final result = await coordinator.updateBaseUrl(
        'http://192.168.31.10:5275/',
      );

      expect(result, isNull);
      expect(client.baseUrl, 'http://192.168.31.10:5275');
      expect(preferences.savedBaseUrls, <String>['http://192.168.31.10:5275']);
      expect(reloadCalls, <bool>[true]);
    },
  );

  test('updateBaseUrl rolls back when reload fails', () async {
    final preferences = _FakeAppPreferences();
    final client = AniNestHttpClient(baseUrl: 'http://localhost:5275');
    final reloadCalls = <String>[];
    final coordinator = BackendConnectionCoordinator(
      appPreferences: preferences,
      client: client,
      reloadFromBackend: ({bool restartHostEvents = false}) async {
        reloadCalls.add(client.baseUrl);
        return reloadCalls.length == 1 ? 'backend unavailable' : null;
      },
    );

    final result = await coordinator.updateBaseUrl(
      'http://192.168.31.10:5275/',
    );

    expect(result, 'backend unavailable');
    expect(client.baseUrl, 'http://localhost:5275');
    expect(preferences.savedBaseUrls, isEmpty);
    expect(reloadCalls, <String>[
      'http://192.168.31.10:5275',
      'http://localhost:5275',
    ]);
  });
}

class _FakeAppPreferences extends AppPreferences {
  String? storedBaseUrl;
  int loadBaseUrlCallCount = 0;
  final savedBaseUrls = <String>[];

  @override
  Future<String?> loadBaseUrl() async {
    loadBaseUrlCallCount += 1;
    return storedBaseUrl;
  }

  @override
  Future<void> saveBaseUrl(String baseUrl) async {
    savedBaseUrls.add(baseUrl);
    storedBaseUrl = baseUrl;
  }
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
