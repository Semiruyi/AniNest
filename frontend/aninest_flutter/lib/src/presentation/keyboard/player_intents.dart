import 'package:flutter/widgets.dart';

class TogglePlayPauseIntent extends Intent {
  const TogglePlayPauseIntent();
}

class SeekRelativeIntent extends Intent {
  const SeekRelativeIntent(this.delta);

  final Duration delta;
}

class AdjustVolumeIntent extends Intent {
  const AdjustVolumeIntent(this.delta);

  final double delta;
}

class ToggleMuteIntent extends Intent {
  const ToggleMuteIntent();
}

class NextEpisodeIntent extends Intent {
  const NextEpisodeIntent();
}

class PreviousEpisodeIntent extends Intent {
  const PreviousEpisodeIntent();
}

class ToggleFullscreenIntent extends Intent {
  const ToggleFullscreenIntent();
}

class ExitFullscreenIntent extends Intent {
  const ExitFullscreenIntent();
}

class CyclePlaybackRateIntent extends Intent {
  const CyclePlaybackRateIntent();
}
