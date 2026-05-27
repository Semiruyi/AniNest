import 'package:aninest_flutter/src/models/enums.dart';

class PlaylistItemDto {
  const PlaylistItemDto({
    required this.itemId,
    required this.index,
    required this.title,
    required this.filePath,
    required this.isPlayed,
    required this.hasSavedProgress,
    required this.savedProgressMs,
    required this.durationMs,
    required this.thumbnailState,
  });

  final String itemId;
  final int index;
  final String title;
  final String filePath;
  final bool isPlayed;
  final bool hasSavedProgress;
  final int savedProgressMs;
  final int durationMs;
  final ThumbnailState thumbnailState;

  factory PlaylistItemDto.fromJson(Map<String, dynamic> json) {
    return PlaylistItemDto(
      itemId: json['itemId'] as String,
      index: json['index'] as int? ?? 0,
      title: json['title'] as String? ?? json['itemId'] as String,
      filePath: json['filePath'] as String,
      isPlayed: json['isPlayed'] as bool? ?? false,
      hasSavedProgress: json['hasSavedProgress'] as bool? ?? false,
      savedProgressMs: json['savedProgressMs'] as int? ?? 0,
      durationMs: json['durationMs'] as int? ?? 0,
      thumbnailState: ThumbnailState.fromJson(json['thumbnailState']),
    );
  }

  PlaylistItemDto copyWith({
    String? itemId,
    int? index,
    String? title,
    String? filePath,
    bool? isPlayed,
    bool? hasSavedProgress,
    int? savedProgressMs,
    int? durationMs,
    ThumbnailState? thumbnailState,
  }) {
    return PlaylistItemDto(
      itemId: itemId ?? this.itemId,
      index: index ?? this.index,
      title: title ?? this.title,
      filePath: filePath ?? this.filePath,
      isPlayed: isPlayed ?? this.isPlayed,
      hasSavedProgress: hasSavedProgress ?? this.hasSavedProgress,
      savedProgressMs: savedProgressMs ?? this.savedProgressMs,
      durationMs: durationMs ?? this.durationMs,
      thumbnailState: thumbnailState ?? this.thumbnailState,
    );
  }
}

class PlaylistDto {
  const PlaylistDto({
    required this.folderId,
    required this.folderName,
    required this.currentItemId,
    required this.currentIndex,
    required this.items,
  });

  final String folderId;
  final String folderName;
  final String? currentItemId;
  final int currentIndex;
  final List<PlaylistItemDto> items;

  factory PlaylistDto.fromJson(Map<String, dynamic> json) {
    return PlaylistDto(
      folderId: json['folderId'] as String,
      folderName: json['folderName'] as String? ?? json['folderId'] as String,
      currentItemId: json['currentItemId'] as String?,
      currentIndex: json['currentIndex'] as int? ?? 0,
      items: (json['items'] as List<dynamic>? ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(PlaylistItemDto.fromJson)
          .toList(),
    );
  }

  PlaylistDto copyWith({
    String? folderId,
    String? folderName,
    String? currentItemId,
    int? currentIndex,
    List<PlaylistItemDto>? items,
  }) {
    return PlaylistDto(
      folderId: folderId ?? this.folderId,
      folderName: folderName ?? this.folderName,
      currentItemId: currentItemId ?? this.currentItemId,
      currentIndex: currentIndex ?? this.currentIndex,
      items: items ?? this.items,
    );
  }
}
