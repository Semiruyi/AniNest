import 'dart:async';

import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';
import 'package:aninest_flutter/src/models/thumbnail_models.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:flutter/foundation.dart';

class MetadataController extends ChangeNotifier {
  MetadataController(this._metadataApi, this._thumbnailApi);

  MetadataApi _metadataApi;
  ThumbnailApi _thumbnailApi;

  MetadataStatusSummaryDto? _metadataSummary;
  MetadataDto? _metadata;
  ThumbnailFolderSummaryDto? _thumbnailSummary;
  List<ThumbnailStatusDto> _thumbnails = const [];

  MetadataStatusSummaryDto? get metadataSummary => _metadataSummary;
  MetadataDto? get metadata => _metadata;
  ThumbnailFolderSummaryDto? get thumbnailSummary => _thumbnailSummary;
  List<ThumbnailStatusDto> get thumbnails => _thumbnails;

  void rebind(MetadataApi metadataApi, ThumbnailApi thumbnailApi) {
    _metadataApi = metadataApi;
    _thumbnailApi = thumbnailApi;
  }

  Future<void> refresh(String? folderId) async {
    final summaryFuture = _metadataApi.getSummary();

    if (folderId == null) {
      _metadataSummary = await summaryFuture;
      _metadata = null;
      _thumbnailSummary = null;
      _thumbnails = const [];
      notifyListeners();
      return;
    }

    final metadataFuture = _loadFolderMetadata(folderId);
    final thumbnailsFuture = _loadFolderThumbnails(folderId);

    _metadataSummary = await summaryFuture;
    final metadataResult = await metadataFuture;
    final thumbnailsResult = await thumbnailsFuture;
    _metadata = metadataResult;
    _thumbnailSummary = thumbnailsResult.summary;
    _thumbnails = thumbnailsResult.thumbnails;
    notifyListeners();
  }

  Future<MetadataDto?> _loadFolderMetadata(String folderId) async {
    try {
      return await _metadataApi.getFolder(folderId);
    } on ApiException {
      return null;
    }
  }

  Future<_ThumbnailRefreshResult> _loadFolderThumbnails(String folderId) async {
    try {
      final results = await Future.wait<Object?>(<Future<Object?>>[
        _thumbnailApi.getFolderSummary(folderId),
        _thumbnailApi.getFolder(folderId),
      ]);
      return _ThumbnailRefreshResult(
        summary: results[0] as ThumbnailFolderSummaryDto,
        thumbnails: results[1] as List<ThumbnailStatusDto>,
      );
    } on ApiException {
      return const _ThumbnailRefreshResult(
        summary: null,
        thumbnails: <ThumbnailStatusDto>[],
      );
    }
  }

  void clear() {
    final hadState =
        _metadataSummary != null ||
        _metadata != null ||
        _thumbnailSummary != null ||
        _thumbnails.isNotEmpty;
    _metadataSummary = null;
    _metadata = null;
    _thumbnailSummary = null;
    _thumbnails = const [];
    if (hadState) {
      notifyListeners();
    }
  }

  void applySummary(MetadataStatusSummaryDto summary) {
    _metadataSummary = summary;
    notifyListeners();
  }

  Future<void> refreshSelectedFolder(String? folderId) async {
    if (folderId == null) {
      return;
    }

    final metadataFuture = _loadFolderMetadata(folderId);
    final thumbnailsFuture = _loadFolderThumbnails(folderId);
    _metadata = await metadataFuture;
    final thumbnailsResult = await thumbnailsFuture;
    _thumbnailSummary = thumbnailsResult.summary;
    _thumbnails = thumbnailsResult.thumbnails;

    notifyListeners();
  }

  void applyFolderUpdate(
    MetadataFolderUpdatedEventDto update,
    String? selectedFolderId,
  ) {
    if (update.folderId != selectedFolderId) {
      return;
    }

    _metadata = MetadataDto(
      folderId: update.folderId,
      title: update.matchedTitle,
      originalTitle: update.originalTitle ?? _metadata?.originalTitle,
      summary: _metadata?.summary,
      tags: _metadata?.tags ?? const [],
      posterPath: update.posterUrl,
      airDate: _metadata?.airDate,
      year: _metadata?.year,
      rating: _metadata?.rating,
      season: _metadata?.season,
      episodeCount: _metadata?.episodeCount,
      source: _metadata?.source,
      state: update.state,
      failureKind: update.failureKind,
    );
    notifyListeners();
  }
}

class _ThumbnailRefreshResult {
  const _ThumbnailRefreshResult({
    required this.summary,
    required this.thumbnails,
  });

  final ThumbnailFolderSummaryDto? summary;
  final List<ThumbnailStatusDto> thumbnails;
}
