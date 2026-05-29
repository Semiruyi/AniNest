import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:flutter/widgets.dart';

import 'player_intents.dart';

Map<Type, Action<Intent>> buildPlayerActions({
  required AppController controller,
  required bool isFullscreen,
  required VoidCallback onToggleFullscreen,
}) {
  return <Type, Action<Intent>>{
    TogglePlayPauseIntent: CallbackAction<TogglePlayPauseIntent>(
      onInvoke: (TogglePlayPauseIntent intent) {
        unawaited(controller.togglePlayPause());
        return null;
      },
    ),
    SeekRelativeIntent: CallbackAction<SeekRelativeIntent>(
      onInvoke: (SeekRelativeIntent intent) {
        unawaited(controller.seekBy(intent.delta));
        return null;
      },
    ),
    AdjustVolumeIntent: CallbackAction<AdjustVolumeIntent>(
      onInvoke: (AdjustVolumeIntent intent) {
        unawaited(controller.adjustPlaybackVolumeBy(intent.delta));
        return null;
      },
    ),
    ToggleMuteIntent: CallbackAction<ToggleMuteIntent>(
      onInvoke: (ToggleMuteIntent intent) {
        unawaited(controller.toggleMute());
        return null;
      },
    ),
    PreviousEpisodeIntent: CallbackAction<PreviousEpisodeIntent>(
      onInvoke: (PreviousEpisodeIntent intent) {
        unawaited(controller.movePreviousAndPlay());
        return null;
      },
    ),
    NextEpisodeIntent: CallbackAction<NextEpisodeIntent>(
      onInvoke: (NextEpisodeIntent intent) {
        unawaited(controller.moveNextAndPlay());
        return null;
      },
    ),
    CyclePlaybackRateIntent: CallbackAction<CyclePlaybackRateIntent>(
      onInvoke: (CyclePlaybackRateIntent intent) {
        unawaited(controller.cyclePlaybackRate());
        return null;
      },
    ),
    ToggleFullscreenIntent: CallbackAction<ToggleFullscreenIntent>(
      onInvoke: (ToggleFullscreenIntent intent) {
        onToggleFullscreen();
        return null;
      },
    ),
    ExitFullscreenIntent: CallbackAction<ExitFullscreenIntent>(
      onInvoke: (ExitFullscreenIntent intent) {
        if (isFullscreen) {
          onToggleFullscreen();
        }
        return null;
      },
    ),
  };
}
