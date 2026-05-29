import 'package:flutter/widgets.dart';
import 'package:flutter/services.dart';

import 'player_intents.dart';

final class PlayerShortcuts {
  PlayerShortcuts._();

  static const Map<ShortcutActivator, Intent> bindings =
      <ShortcutActivator, Intent>{
        SingleActivator(LogicalKeyboardKey.space): TogglePlayPauseIntent(),
        SingleActivator(LogicalKeyboardKey.keyK): TogglePlayPauseIntent(),
        SingleActivator(LogicalKeyboardKey.arrowLeft): SeekRelativeIntent(
          Duration(seconds: -5),
        ),
        SingleActivator(LogicalKeyboardKey.arrowRight): SeekRelativeIntent(
          Duration(seconds: 5),
        ),
        SingleActivator(LogicalKeyboardKey.arrowLeft, shift: true):
            SeekRelativeIntent(Duration(seconds: -15)),
        SingleActivator(LogicalKeyboardKey.arrowRight, shift: true):
            SeekRelativeIntent(Duration(seconds: 15)),
        SingleActivator(LogicalKeyboardKey.arrowUp): AdjustVolumeIntent(5),
        SingleActivator(LogicalKeyboardKey.arrowDown): AdjustVolumeIntent(-5),
        SingleActivator(LogicalKeyboardKey.keyM): ToggleMuteIntent(),
        SingleActivator(LogicalKeyboardKey.keyF): ToggleFullscreenIntent(),
        SingleActivator(LogicalKeyboardKey.escape): ExitFullscreenIntent(),
        SingleActivator(LogicalKeyboardKey.pageUp): PreviousEpisodeIntent(),
        SingleActivator(LogicalKeyboardKey.pageDown): NextEpisodeIntent(),
        SingleActivator(LogicalKeyboardKey.keyS): CyclePlaybackRateIntent(),
      };
}
