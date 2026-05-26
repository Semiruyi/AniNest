class PlaybackTargetDto {
  const PlaybackTargetDto({
    required this.itemId,
    required this.title,
    required this.mediaUrl,
    required this.startPositionMs,
    required this.subtitleUrl,
    required this.audioTrackHint,
  });

  final String itemId;
  final String title;
  final String mediaUrl;
  final int startPositionMs;
  final String? subtitleUrl;
  final String? audioTrackHint;

  factory PlaybackTargetDto.fromJson(Map<String, dynamic> json) {
    return PlaybackTargetDto(
      itemId: json['itemId'] as String,
      title: json['title'] as String,
      mediaUrl: json['mediaUrl'] as String,
      startPositionMs: json['startPositionMs'] as int? ?? 0,
      subtitleUrl: json['subtitleUrl'] as String?,
      audioTrackHint: json['audioTrackHint'] as String?,
    );
  }

  PlaybackTargetDto copyWith({
    String? itemId,
    String? title,
    String? mediaUrl,
    int? startPositionMs,
    String? subtitleUrl,
    String? audioTrackHint,
  }) {
    return PlaybackTargetDto(
      itemId: itemId ?? this.itemId,
      title: title ?? this.title,
      mediaUrl: mediaUrl ?? this.mediaUrl,
      startPositionMs: startPositionMs ?? this.startPositionMs,
      subtitleUrl: subtitleUrl ?? this.subtitleUrl,
      audioTrackHint: audioTrackHint ?? this.audioTrackHint,
    );
  }
}

class SessionStateDto {
  const SessionStateDto({
    required this.sessionId,
    required this.folderId,
    required this.folderName,
    required this.currentItemId,
    required this.currentIndex,
    required this.playlistCount,
    required this.hasPrevious,
    required this.hasNext,
    required this.savedProgressMs,
    required this.preferredRate,
    required this.preferredVolume,
  });

  final String sessionId;
  final String folderId;
  final String folderName;
  final String currentItemId;
  final int currentIndex;
  final int playlistCount;
  final bool hasPrevious;
  final bool hasNext;
  final int savedProgressMs;
  final double preferredRate;
  final int preferredVolume;

  factory SessionStateDto.fromJson(Map<String, dynamic> json) {
    return SessionStateDto(
      sessionId: json['sessionId'] as String,
      folderId: json['folderId'] as String,
      folderName: json['folderName'] as String,
      currentItemId: json['currentItemId'] as String,
      currentIndex: json['currentIndex'] as int? ?? 0,
      playlistCount: json['playlistCount'] as int? ?? 0,
      hasPrevious: json['hasPrevious'] as bool? ?? false,
      hasNext: json['hasNext'] as bool? ?? false,
      savedProgressMs: json['savedProgressMs'] as int? ?? 0,
      preferredRate: (json['preferredRate'] as num?)?.toDouble() ?? 1,
      preferredVolume: json['preferredVolume'] as int? ?? 80,
    );
  }

  SessionStateDto copyWith({
    String? sessionId,
    String? folderId,
    String? folderName,
    String? currentItemId,
    int? currentIndex,
    int? playlistCount,
    bool? hasPrevious,
    bool? hasNext,
    int? savedProgressMs,
    double? preferredRate,
    int? preferredVolume,
  }) {
    return SessionStateDto(
      sessionId: sessionId ?? this.sessionId,
      folderId: folderId ?? this.folderId,
      folderName: folderName ?? this.folderName,
      currentItemId: currentItemId ?? this.currentItemId,
      currentIndex: currentIndex ?? this.currentIndex,
      playlistCount: playlistCount ?? this.playlistCount,
      hasPrevious: hasPrevious ?? this.hasPrevious,
      hasNext: hasNext ?? this.hasNext,
      savedProgressMs: savedProgressMs ?? this.savedProgressMs,
      preferredRate: preferredRate ?? this.preferredRate,
      preferredVolume: preferredVolume ?? this.preferredVolume,
    );
  }
}

class SessionOpenResultDto {
  const SessionOpenResultDto({
    required this.session,
    required this.playbackTarget,
  });

  final SessionStateDto session;
  final PlaybackTargetDto playbackTarget;

  factory SessionOpenResultDto.fromJson(Map<String, dynamic> json) {
    return SessionOpenResultDto(
      session: SessionStateDto.fromJson(
        json['session'] as Map<String, dynamic>,
      ),
      playbackTarget: PlaybackTargetDto.fromJson(
        json['playbackTarget'] as Map<String, dynamic>,
      ),
    );
  }
}
