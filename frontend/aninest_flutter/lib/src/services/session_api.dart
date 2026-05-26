import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/session_models.dart';

class SessionApi {
  const SessionApi(this._client);

  final AniNestHttpClient _client;

  Future<SessionStateDto?> getCurrent() async {
    final payload = await _client.get('/api/session');
    if (payload == null) {
      return null;
    }

    return SessionStateDto.fromJson((payload as Map).cast<String, dynamic>());
  }

  Future<SessionOpenResultDto> openFolder(String folderId) async {
    final payload = await _client.post(
      '/api/session/open-folder',
      body: <String, dynamic>{'folderId': folderId},
    );
    return _resolveSessionOpenResult((payload as Map).cast<String, dynamic>());
  }

  Future<SessionOpenResultDto> selectItem(String itemId) async {
    final payload = await _client.post(
      '/api/playlist/current/items/$itemId:select',
    );
    return _resolveSessionOpenResult((payload as Map).cast<String, dynamic>());
  }

  Future<SessionOpenResultDto> moveNext() async {
    final payload = await _client.post('/api/session/next');
    return _resolveSessionOpenResult((payload as Map).cast<String, dynamic>());
  }

  Future<SessionOpenResultDto> movePrevious() async {
    final payload = await _client.post('/api/session/previous');
    return _resolveSessionOpenResult((payload as Map).cast<String, dynamic>());
  }

  Future<void> close() async {
    await _client.post('/api/session/close');
  }

  SessionOpenResultDto _resolveSessionOpenResult(Map<String, dynamic> json) {
    final result = SessionOpenResultDto.fromJson(json);
    final target = result.playbackTarget;

    return SessionOpenResultDto(
      session: result.session,
      playbackTarget: target.copyWith(
        mediaUrl: _client.resolveUrl(target.mediaUrl) ?? target.mediaUrl,
        subtitleUrl: _client.resolveUrl(target.subtitleUrl),
      ),
    );
  }
}
