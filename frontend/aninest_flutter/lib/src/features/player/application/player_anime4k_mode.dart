enum PlayerAnime4kMode {
  off,
  fast,
  highQuality;

  static PlayerAnime4kMode fromId(String? value) {
    return PlayerAnime4kMode.values.firstWhere(
      (PlayerAnime4kMode mode) => mode.id == value,
      orElse: () => PlayerAnime4kMode.off,
    );
  }

  bool get isEnabled => this != PlayerAnime4kMode.off;

  String get id => switch (this) {
    PlayerAnime4kMode.off => 'off',
    PlayerAnime4kMode.fast => 'fast',
    PlayerAnime4kMode.highQuality => 'high_quality',
  };

  String get label => switch (this) {
    PlayerAnime4kMode.off => 'Anime4K Off',
    PlayerAnime4kMode.fast => 'Anime4K Fast',
    PlayerAnime4kMode.highQuality => 'Anime4K HQ',
  };

  String get shortLabel => switch (this) {
    PlayerAnime4kMode.off => 'A4K',
    PlayerAnime4kMode.fast => 'A4K F',
    PlayerAnime4kMode.highQuality => 'A4K HQ',
  };
}
