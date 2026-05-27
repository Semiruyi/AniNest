import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_option.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerVideoViewport extends StatelessWidget {
  const PlayerVideoViewport({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (BuildContext context, Widget? child) {
        final colorScheme = Theme.of(context).colorScheme;
        final runtime = controller.runtime;
        final showVideo = controller.playbackTarget != null || runtime.hasMedia;

        return Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (showVideo)
              Video(
                controller: controller.videoController,
                controls: NoVideoControls,
                fit: BoxFit.contain,
                fill: colorScheme.background,
                subtitleViewConfiguration: SubtitleViewConfiguration(
                  visible:
                      runtime.selectedSubtitleTrackId !=
                      PlayerSubtitleTrackOption.offId,
                ),
              ),
            if (runtime.isLoading)
              const Center(
                child: SizedBox(
                  width: 24,
                  height: 24,
                  child: CircularProgressIndicator(),
                ),
              ),
          ],
        );
      },
    );
  }
}
