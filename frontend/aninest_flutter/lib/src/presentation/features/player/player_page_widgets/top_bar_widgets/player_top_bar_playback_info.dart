import 'package:aninest_flutter/src/models/playlist_models.dart';

import '../player_view_state_controller.dart';

class PlayerTopBarPlaybackInfo {
  const PlayerTopBarPlaybackInfo({
    required this.currentEpisodeNumber,
    required this.totalEpisodeCount,
    required this.videoFormat,
    required this.fileName,
    required this.filePath,
  });

  final int? currentEpisodeNumber;
  final int totalEpisodeCount;
  final String videoFormat;
  final String? fileName;
  final String? filePath;

  bool get hasSelectedMedia => fileName != null;

  String get episodeLabel {
    if (totalEpisodeCount <= 0) {
      return '-- / --';
    }

    final current = currentEpisodeNumber;
    if (current == null) {
      return '-- / $totalEpisodeCount';
    }

    final width = totalEpisodeCount.toString().length;
    return '${current.toString().padLeft(width, '0')} / $totalEpisodeCount';
  }

  factory PlayerTopBarPlaybackInfo.fromController(
    PlayerViewStateController controller,
  ) {
    final playlist = controller.playlist;
    final items = playlist?.items ?? const <PlaylistItemDto>[];
    final selectedItemId = controller.selectedItemId;
    final selectedIndex = _selectedIndex(
      items: items,
      selectedItemId: selectedItemId,
      playlistCurrentIndex: playlist?.currentIndex,
    );
    final item = selectedIndex == null ? null : items[selectedIndex];
    final fileName = item == null ? null : _fileNameFromPath(item.filePath);

    return PlayerTopBarPlaybackInfo(
      currentEpisodeNumber: selectedIndex == null ? null : selectedIndex + 1,
      totalEpisodeCount: items.length,
      videoFormat: _formatFromFileName(fileName),
      fileName: fileName,
      filePath: item?.filePath,
    );
  }

  static int? _selectedIndex({
    required List<PlaylistItemDto> items,
    required String? selectedItemId,
    required int? playlistCurrentIndex,
  }) {
    if (items.isEmpty) {
      return null;
    }

    if (selectedItemId != null) {
      final index = items.indexWhere(
        (PlaylistItemDto item) => item.itemId == selectedItemId,
      );
      if (index >= 0) {
        return index;
      }
    }

    if (playlistCurrentIndex != null &&
        playlistCurrentIndex >= 0 &&
        playlistCurrentIndex < items.length) {
      return playlistCurrentIndex;
    }

    return null;
  }

  static String _fileNameFromPath(String filePath) {
    final normalized = filePath.replaceAll('\\', '/');
    final segments = normalized
        .split('/')
        .where((String segment) => segment.isNotEmpty)
        .toList();
    return segments.isEmpty ? filePath : segments.last;
  }

  static String _formatFromFileName(String? fileName) {
    if (fileName == null) {
      return 'VIDEO';
    }

    final extensionStart = fileName.lastIndexOf('.');
    if (extensionStart <= 0 || extensionStart == fileName.length - 1) {
      return 'VIDEO';
    }

    final extension = fileName.substring(extensionStart + 1).trim();
    if (extension.isEmpty || extension.length > 10) {
      return 'VIDEO';
    }

    return extension.toUpperCase();
  }
}
