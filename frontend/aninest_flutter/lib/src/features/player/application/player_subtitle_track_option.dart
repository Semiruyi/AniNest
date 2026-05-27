class PlayerSubtitleTrackOption {
  const PlayerSubtitleTrackOption({
    required this.id,
    required this.title,
    required this.language,
    required this.codec,
    required this.kind,
    required this.index,
  });

  static const String automaticId = 'auto';
  static const String offId = 'no';

  final String id;
  final String? title;
  final String? language;
  final String? codec;
  final PlayerSubtitleTrackKind kind;
  final int index;

  bool get isAutomatic => kind == PlayerSubtitleTrackKind.automatic;
  bool get isOff => kind == PlayerSubtitleTrackKind.off;
}

enum PlayerSubtitleTrackKind { automatic, off, embedded, external }
