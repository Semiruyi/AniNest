import 'dart:convert';

import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:http/http.dart' as http;

class AniNestHttpClient {
  AniNestHttpClient({required String baseUrl, http.Client? httpClient})
    : _baseUrl = normalizeBaseUrl(baseUrl),
      _httpClient = httpClient ?? http.Client();

  final http.Client _httpClient;
  String _baseUrl;

  String get baseUrl => _baseUrl;

  void updateBaseUrl(String nextBaseUrl) {
    _baseUrl = normalizeBaseUrl(nextBaseUrl);
  }

  String? resolveUrl(String? path) {
    if (path == null || path.isEmpty) {
      return null;
    }

    return _resolve(path).toString();
  }

  Future<dynamic> get(String path) => _send('GET', path);

  Future<Map<String, dynamic>> getObject(String path) async {
    final payload = await _send('GET', path);
    return (payload as Map).cast<String, dynamic>();
  }

  Future<List<dynamic>> getList(String path) async {
    final payload = await _send('GET', path);
    return payload as List<dynamic>;
  }

  Future<dynamic> post(String path, {Object? body}) =>
      _send('POST', path, body: body);

  Future<dynamic> put(String path, {Object? body}) =>
      _send('PUT', path, body: body);

  Future<dynamic> delete(String path) => _send('DELETE', path);

  Future<http.StreamedResponse> openGetStream(
    String path, {
    Map<String, String>? headers,
  }) {
    final request = http.Request('GET', _resolve(path));
    if (headers != null) {
      request.headers.addAll(headers);
    }
    return _httpClient.send(request);
  }

  void close() {
    _httpClient.close();
  }

  Future<dynamic> _send(String method, String path, {Object? body}) async {
    final requestStopwatch = Stopwatch()..start();
    final request = http.Request(method, _resolve(path));
    request.headers['Accept'] = 'application/json';
    if (body != null) {
      request.headers['Content-Type'] = 'application/json';
      request.body = jsonEncode(body);
    }

    try {
      final sendStopwatch = Stopwatch()..start();
      final streamed = await _httpClient.send(request);
      sendStopwatch.stop();

      final responseStopwatch = Stopwatch()..start();
      final response = await http.Response.fromStream(streamed);
      responseStopwatch.stop();

      final decodeStopwatch = Stopwatch()..start();
      final decoded = _decode(response);
      decodeStopwatch.stop();
      requestStopwatch.stop();

      AppLogger.info(
        'HttpClient',
        '$method $path status=${response.statusCode} send=${sendStopwatch.elapsedMilliseconds}ms receive=${responseStopwatch.elapsedMilliseconds}ms decode=${decodeStopwatch.elapsedMilliseconds}ms total=${requestStopwatch.elapsedMilliseconds}ms',
      );

      if (response.statusCode >= 400) {
        if (decoded is Map<String, dynamic>) {
          throw ApiException(
            statusCode: response.statusCode,
            code: decoded['code'] as String? ?? 'api.error',
            message: decoded['message'] as String? ?? 'Unknown API error.',
            details: decoded['details'] as Map<String, dynamic>?,
          );
        }

        throw ApiException(
          statusCode: response.statusCode,
          code: 'api.error',
          message: decoded?.toString() ?? 'Unknown API error.',
        );
      }

      return decoded;
    } catch (error, stackTrace) {
      requestStopwatch.stop();
      AppLogger.error(
        'HttpClient',
        '$method $path failed after ${requestStopwatch.elapsedMilliseconds}ms',
        error: error,
        stackTrace: stackTrace,
      );
      rethrow;
    }
  }

  dynamic _decode(http.Response response) {
    if (response.body.isEmpty) {
      return null;
    }

    final contentType = response.headers['content-type'] ?? '';
    if (!contentType.contains('application/json')) {
      return response.body;
    }

    return jsonDecode(response.body);
  }

  Uri _resolve(String path) {
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return Uri.parse(path);
    }

    final normalizedPath = path.startsWith('/') ? path.substring(1) : path;
    return Uri.parse('$_baseUrl/$normalizedPath');
  }

  static bool isValidBaseUrl(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return false;
    }

    final uri = Uri.tryParse(trimmed);
    if (uri == null || !uri.hasScheme || uri.host.isEmpty) {
      return false;
    }

    return uri.scheme == 'http' || uri.scheme == 'https';
  }

  static String normalizeBaseUrl(String value) {
    final trimmed = value.trim();
    return trimmed.endsWith('/')
        ? trimmed.substring(0, trimmed.length - 1)
        : trimmed;
  }
}
