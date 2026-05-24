import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';

String displayLibraryFolderTitle(LibraryFolderDto folder) {
  final metadataTitle = folder.metadataSummary?.title;
  if (metadataTitle != null && metadataTitle.trim().isNotEmpty) {
    return metadataTitle;
  }
  return folder.name;
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
