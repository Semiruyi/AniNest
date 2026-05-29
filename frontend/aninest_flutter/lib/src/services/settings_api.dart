import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';

class SettingsApi {
  const SettingsApi(this._client);

  final AniNestHttpClient _client;

  Future<AppSettingsDto> getAll() async {
    final payload = await _client.getObject('/api/settings');
    return AppSettingsDto.fromJson(payload);
  }

  Future<PlayerSettingsDto> getPlayer() async {
    final payload = await _client.getObject('/api/settings/player');
    return PlayerSettingsDto.fromJson(payload);
  }

  Future<void> savePlayer(PlayerSettingsDto settings) async {
    await _client.put('/api/settings/player', body: settings.toJson());
  }

  Future<MetadataSettingsDto> getMetadata() async {
    final payload = await _client.getObject('/api/settings/metadata');
    return MetadataSettingsDto.fromJson(payload);
  }

  Future<void> saveMetadata(MetadataSettingsDto settings) async {
    await _client.put('/api/settings/metadata', body: settings.toJson());
  }
}
