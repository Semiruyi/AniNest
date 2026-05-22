class PlayerSettingsDto {
  const PlayerSettingsDto({
    required this.preferredRate,
    required this.preferredVolume,
    required this.resumePlayback,
  });

  final double preferredRate;
  final int preferredVolume;
  final bool resumePlayback;

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'preferredRate': preferredRate,
      'preferredVolume': preferredVolume,
      'resumePlayback': resumePlayback,
    };
  }

  factory PlayerSettingsDto.fromJson(Map<String, dynamic> json) {
    return PlayerSettingsDto(
      preferredRate: (json['preferredRate'] as num?)?.toDouble() ?? 1,
      preferredVolume: json['preferredVolume'] as int? ?? 80,
      resumePlayback: json['resumePlayback'] as bool? ?? true,
    );
  }
}

class MetadataSettingsDto {
  const MetadataSettingsDto({required this.autoScrapeMetadata});

  final bool autoScrapeMetadata;

  factory MetadataSettingsDto.fromJson(Map<String, dynamic> json) {
    return MetadataSettingsDto(
      autoScrapeMetadata: json['autoScrapeMetadata'] as bool? ?? false,
    );
  }
}

class ThumbnailSettingsDto {
  const ThumbnailSettingsDto({
    required this.expiryDays,
    required this.generateOnImport,
  });

  final int expiryDays;
  final bool generateOnImport;

  factory ThumbnailSettingsDto.fromJson(Map<String, dynamic> json) {
    return ThumbnailSettingsDto(
      expiryDays: json['expiryDays'] as int? ?? 30,
      generateOnImport: json['generateOnImport'] as bool? ?? false,
    );
  }
}

class AppSettingsDto {
  const AppSettingsDto({
    required this.player,
    required this.metadata,
    required this.thumbnails,
  });

  final PlayerSettingsDto player;
  final MetadataSettingsDto metadata;
  final ThumbnailSettingsDto thumbnails;

  factory AppSettingsDto.fromJson(Map<String, dynamic> json) {
    return AppSettingsDto(
      player: PlayerSettingsDto.fromJson(
        json['player'] as Map<String, dynamic>,
      ),
      metadata: MetadataSettingsDto.fromJson(
        json['metadata'] as Map<String, dynamic>,
      ),
      thumbnails: ThumbnailSettingsDto.fromJson(
        json['thumbnails'] as Map<String, dynamic>,
      ),
    );
  }
}
