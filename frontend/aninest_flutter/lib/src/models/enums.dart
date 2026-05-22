enum WatchStatus {
  unknown,
  watching,
  completed,
  onHold,
  dropped,
  planned;

  static WatchStatus fromJson(Object? value) {
    return switch (value) {
      1 => WatchStatus.watching,
      2 => WatchStatus.completed,
      3 => WatchStatus.onHold,
      4 => WatchStatus.dropped,
      5 => WatchStatus.planned,
      String text => WatchStatus.values.firstWhere(
        (item) => item.name.toLowerCase() == _normalize(text),
        orElse: () => WatchStatus.unknown,
      ),
      _ => WatchStatus.unknown,
    };
  }
}

enum MetadataState {
  unknown,
  needsMetadata,
  queued,
  scraping,
  ready,
  needsReview,
  disabled;

  static MetadataState fromJson(Object? value) {
    return switch (value) {
      1 => MetadataState.needsMetadata,
      2 => MetadataState.queued,
      3 => MetadataState.scraping,
      4 => MetadataState.ready,
      5 => MetadataState.needsReview,
      6 => MetadataState.disabled,
      String text => MetadataState.values.firstWhere(
        (item) => item.name.toLowerCase() == _normalize(text),
        orElse: () => MetadataState.unknown,
      ),
      _ => MetadataState.unknown,
    };
  }
}

enum MetadataFailureKind {
  none,
  networkError,
  noMatch,
  providerError;

  static MetadataFailureKind fromJson(Object? value) {
    return switch (value) {
      1 => MetadataFailureKind.networkError,
      2 => MetadataFailureKind.noMatch,
      3 => MetadataFailureKind.providerError,
      String text => MetadataFailureKind.values.firstWhere(
        (item) => item.name.toLowerCase() == _normalize(text),
        orElse: () => MetadataFailureKind.none,
      ),
      _ => MetadataFailureKind.none,
    };
  }
}

enum ThumbnailState {
  unknown,
  pending,
  generating,
  ready,
  failed;

  static ThumbnailState fromJson(Object? value) {
    return switch (value) {
      1 => ThumbnailState.pending,
      2 => ThumbnailState.generating,
      3 => ThumbnailState.ready,
      4 => ThumbnailState.failed,
      String text => ThumbnailState.values.firstWhere(
        (item) => item.name.toLowerCase() == _normalize(text),
        orElse: () => ThumbnailState.unknown,
      ),
      _ => ThumbnailState.unknown,
    };
  }
}

String _normalize(String value) {
  return value
      .replaceAll('_', '')
      .replaceAll('-', '')
      .replaceAll(' ', '')
      .toLowerCase();
}
