import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('AniNestHttpClient keeps a normalized base url', () {
    final client = AniNestHttpClient(baseUrl: 'http://localhost:5275/');

    expect(client.baseUrl, 'http://localhost:5275');

    client.updateBaseUrl('http://127.0.0.1:5275/');

    expect(client.baseUrl, 'http://127.0.0.1:5275');
  });
}
