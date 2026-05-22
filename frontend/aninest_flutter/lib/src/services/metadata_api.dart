import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';

class MetadataApi {
  const MetadataApi(this._client);

  final AniNestHttpClient _client;

  Future<MetadataStatusSummaryDto> getSummary() async {
    final payload = await _client.getObject('/api/metadata/status-summary');
    return MetadataStatusSummaryDto.fromJson(payload);
  }

  Future<MetadataDto> getFolder(String folderId) async {
    final payload = await _client.getObject('/api/metadata/folders/$folderId');
    return MetadataDto.fromJson(payload);
  }
}
