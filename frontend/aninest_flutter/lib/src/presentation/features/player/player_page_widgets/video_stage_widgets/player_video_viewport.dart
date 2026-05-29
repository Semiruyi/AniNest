import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerVideoViewport extends StatelessWidget {
  const PlayerVideoViewport({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller.player,
      builder: (BuildContext context, Widget? child) {
        final colorScheme = Theme.of(context).colorScheme;
        final runtime = controller.playerRuntime;
        final showVideo = controller.playbackTarget != null || runtime.hasMedia;

        return Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (showVideo)
              Video(
                controller: controller.playerVideoController,
                controls: NoVideoControls,
                fit: BoxFit.contain,
                fill: colorScheme.background,
                subtitleViewConfiguration: const SubtitleViewConfiguration(
                  visible: false,
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
