import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/thumbnail_models.dart';

class ThumbnailApi {
  const ThumbnailApi(this._client);

  final AniNestHttpClient _client;

  Future<ThumbnailFolderSummaryDto> getFolderSummary(String folderId) async {
    final payload = await _client.getObject(
      '/api/thumbnails/folders/$folderId/summary',
    );
    return ThumbnailFolderSummaryDto.fromJson(payload);
  }

  Future<List<ThumbnailStatusDto>> getFolder(String folderId) async {
    final payload = await _client.getList('/api/thumbnails/folders/$folderId');
    return payload
        .whereType<Map<String, dynamic>>()
        .map(ThumbnailStatusDto.fromJson)
        .toList();
  }
}
