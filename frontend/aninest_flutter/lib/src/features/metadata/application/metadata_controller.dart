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
    _metadataSummary = await _metadataApi.getSummary();

    if (folderId == null) {
      _metadata = null;
      _thumbnailSummary = null;
      _thumbnails = const [];
      notifyListeners();
      return;
    }

    try {
      _metadata = await _metadataApi.getFolder(folderId);
    } on ApiException {
      _metadata = null;
    }

    try {
      _thumbnailSummary = await _thumbnailApi.getFolderSummary(folderId);
      _thumbnails = await _thumbnailApi.getFolder(folderId);
    } on ApiException {
      _thumbnailSummary = null;
      _thumbnails = const [];
    }

    notifyListeners();
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

    try {
      _metadata = await _metadataApi.getFolder(folderId);
    } on ApiException {
      _metadata = null;
    }

    try {
      _thumbnailSummary = await _thumbnailApi.getFolderSummary(folderId);
      _thumbnails = await _thumbnailApi.getFolder(folderId);
    } on ApiException {
      _thumbnailSummary = null;
      _thumbnails = const [];
    }

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
      title: update.title,
      originalTitle: _metadata?.originalTitle,
      summary: _metadata?.summary,
      tags: _metadata?.tags ?? const [],
      posterPath: update.posterUrl,
      season: _metadata?.season,
      episodeCount: _metadata?.episodeCount,
      source: _metadata?.source,
      state: update.state,
      failureKind: update.failureKind,
    );
    notifyListeners();
  }
}
