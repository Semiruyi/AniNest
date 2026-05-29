import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import '../player_selector.dart';

class PlayerVideoViewport extends StatelessWidget {
  const PlayerVideoViewport({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    return PlayerSelector<({bool showVideo, bool isLoading})>(
      controller: controller,
      selector: (state) => (
        showVideo: state.playbackTarget != null || state.runtime.hasMedia,
        isLoading: state.runtime.isLoading,
      ),
      builder: (BuildContext context, value) {
        final colorScheme = Theme.of(context).colorScheme;

        return Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (value.showVideo)
              Video(
                controller: controller.videoController,
                controls: NoVideoControls,
                fit: BoxFit.contain,
                fill: colorScheme.background,
                subtitleViewConfiguration: const SubtitleViewConfiguration(
                  visible: false,
                ),
              ),
            if (value.isLoading)
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
