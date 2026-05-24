import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';

String displayLibraryFolderTitle(LibraryFolderDto folder) {
  return folder.name;
}

String? displayMetadataTitle(LibraryFolderDto folder) {
  final metadataTitle =
      folder.metadataSummary?.matchedTitle ??
      folder.metadataSummary?.originalTitle;
  if (metadataTitle != null && metadataTitle.trim().isNotEmpty) {
    return metadataTitle;
  }

  return null;
}

String? displayOriginalMetadataTitle(LibraryFolderDto folder) {
  final originalTitle = folder.metadataSummary?.originalTitle;
  if (originalTitle != null && originalTitle.trim().isNotEmpty) {
    return originalTitle;
  }

  return null;
}

String displayMetadataState(LibraryFolderDto folder) {
  final state = folder.metadataSummary?.state;
  if (state == null || state.trim().isEmpty) {
    return 'Unknown';
  }

  return state;
}

String formatMetadataRating(double? rating) {
  if (rating == null) {
    return 'Unknown';
  }

  return rating.toStringAsFixed(1);
}

String formatMetadataTags(List<String> tags) {
  if (tags.isEmpty) {
    return 'None';
  }

  return tags.take(6).join(', ');
}

String watchStatusLabel(WatchStatus status) {
  return switch (status) {
    WatchStatus.watching => 'Watching',
    WatchStatus.completed => 'Completed',
    WatchStatus.onHold => 'On hold',
    WatchStatus.dropped => 'Dropped',
    WatchStatus.planned => 'Planned',
    WatchStatus.unknown => 'Unknown',
  };
}
