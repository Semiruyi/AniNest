import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/library_models.dart';

class LibraryApi {
  const LibraryApi(this._client);

  final AniNestHttpClient _client;

  Future<List<LibraryFolderDto>> getFolders() async {
    final payload = await _client.getObject('/api/library/folders');
    return (payload['items'] as List<dynamic>? ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(LibraryFolderDto.fromJson)
        .toList();
  }

  Future<void> addFolder(String path) async {
    await _client.post(
      '/api/library/folders',
      body: <String, dynamic>{'path': path},
    );
  }

  Future<void> addFolderBatch(String rootPath) async {
    await _client.post(
      '/api/library/folders:batch-add',
      body: <String, dynamic>{'rootPath': rootPath},
    );
  }

  Future<void> setFavorite(String folderId, bool isFavorite) async {
    await _client.post(
      '/api/library/folders/$folderId:favorite',
      body: <String, dynamic>{'isFavorite': isFavorite},
    );
  }
}
