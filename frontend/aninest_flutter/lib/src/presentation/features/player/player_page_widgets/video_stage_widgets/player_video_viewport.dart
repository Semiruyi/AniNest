import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerVideoViewport extends StatefulWidget {
  const PlayerVideoViewport({
    super.key,
    required this.controller,
  });

  final PlayerController controller;

  @override
  State<PlayerVideoViewport> createState() => _PlayerVideoViewportState();
}

class _PlayerVideoViewportState extends State<PlayerVideoViewport> {
  late final Player _player;
  late final VideoController _videoController;

  String? _loadedPlaybackKey;
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _player = Player();
    _videoController = VideoController(_player);
    widget.controller.addListener(_handleControllerChanged);
    _synchronizePlaybackTarget();
  }

  @override
  void didUpdateWidget(covariant PlayerVideoViewport oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.controller == widget.controller) {
      return;
    }

    oldWidget.controller.removeListener(_handleControllerChanged);
    widget.controller.addListener(_handleControllerChanged);
    _loadedPlaybackKey = null;
    _synchronizePlaybackTarget();
  }

  @override
  void dispose() {
    widget.controller.removeListener(_handleControllerChanged);
    _player.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final target = widget.controller.playbackTarget;
    final showVideo = target != null;

    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        if (showVideo)
          Video(
            controller: _videoController,
            controls: NoVideoControls,
            fit: BoxFit.contain,
            fill: colorScheme.background,
            subtitleViewConfiguration: const SubtitleViewConfiguration(
              visible: false,
            ),
          ),
        if (_isLoading)
          const Center(
            child: SizedBox(
              width: 24,
              height: 24,
              child: CircularProgressIndicator(),
            ),
          ),
      ],
    );
  }

  void _handleControllerChanged() {
    _synchronizePlaybackTarget();
  }

  Future<void> _synchronizePlaybackTarget() async {
    final target = widget.controller.playbackTarget;
    final session = widget.controller.session;

    if (target == null || session == null) {
      _loadedPlaybackKey = null;
      await _player.stop();
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
      return;
    }

    final playbackKey =
        '${target.itemId}|${target.mediaUrl}|${target.startPositionMs}|${session.preferredRate}|${session.preferredVolume}';

    if (_loadedPlaybackKey == playbackKey) {
      return;
    }

    if (mounted) {
      setState(() {
        _isLoading = true;
      });
    }

    try {
      await _player.open(
        Media(
          target.mediaUrl,
          start: Duration(milliseconds: target.startPositionMs),
        ),
      );
      await _player.setRate(session.preferredRate);
      await _player.setVolume(session.preferredVolume.toDouble());
      await _player.setSubtitleTrack(SubtitleTrack.no());
      _loadedPlaybackKey = playbackKey;
    } catch (error, stackTrace) {
      debugPrint('Failed to open playback target: $error');
      debugPrintStack(stackTrace: stackTrace);
      _loadedPlaybackKey = null;
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
        });
      }
    }
  }
}
