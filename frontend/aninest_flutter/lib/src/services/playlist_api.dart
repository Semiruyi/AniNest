import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';

class PlaylistApi {
  const PlaylistApi(this._client);

  final AniNestHttpClient _client;

  Future<PlaylistDto> getCurrent() async {
    final payload = await _client.getObject('/api/playlist/current');
    return PlaylistDto.fromJson(payload);
  }
}
