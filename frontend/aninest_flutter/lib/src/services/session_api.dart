import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/session_models.dart';

class SessionApi {
  const SessionApi(this._client);

  final AniNestHttpClient _client;

  Future<SessionStateDto?> getCurrent() async {
    final payload = await _client.getObject('/api/session');
    return SessionStateDto.fromJson(payload);
  }

  Future<SessionOpenResultDto> openFolder(String folderId) async {
    final payload = await _client.post(
      '/api/session/open-folder',
      body: <String, dynamic>{'folderId': folderId},
    );
    return SessionOpenResultDto.fromJson(
      (payload as Map).cast<String, dynamic>(),
    );
  }

  Future<SessionOpenResultDto> selectItem(String itemId) async {
    final payload = await _client.post(
      '/api/playlist/current/items/$itemId:select',
    );
    return SessionOpenResultDto.fromJson(
      (payload as Map).cast<String, dynamic>(),
    );
  }

  Future<SessionOpenResultDto> moveNext() async {
    final payload = await _client.post('/api/session/next');
    return SessionOpenResultDto.fromJson(
      (payload as Map).cast<String, dynamic>(),
    );
  }

  Future<SessionOpenResultDto> movePrevious() async {
    final payload = await _client.post('/api/session/previous');
    return SessionOpenResultDto.fromJson(
      (payload as Map).cast<String, dynamic>(),
    );
  }

  Future<void> close() async {
    await _client.post('/api/session/close');
  }
}
