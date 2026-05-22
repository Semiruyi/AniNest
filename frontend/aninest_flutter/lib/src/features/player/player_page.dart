import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:flutter/material.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

class PlayerPage extends StatelessWidget {
  const PlayerPage({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (context, _) {
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                flex: 5,
                child: Column(
                  children: [
                    Expanded(
                      child: _VideoSurface(
                        playbackTarget: controller.playbackTarget,
                      ),
                    ),
                    const SizedBox(height: 12),
                    _SessionToolbar(controller: controller),
                  ],
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                flex: 4,
                child: Column(
                  children: [
                    _SessionCard(
                      session: controller.session,
                      target: controller.playbackTarget,
                    ),
                    const SizedBox(height: 12),
                    Expanded(
                      child: _PlaylistCard(
                        playlist: controller.playlist,
                        currentItemId: controller.session?.currentItemId,
                        onSelect: controller.selectItem,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _SessionToolbar extends StatelessWidget {
  const _SessionToolbar({required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            FilledButton.tonalIcon(
              onPressed: controller.session == null
                  ? null
                  : controller.movePrevious,
              icon: const Icon(Icons.skip_previous),
              label: const Text('Previous'),
            ),
            const SizedBox(width: 8),
            FilledButton.icon(
              onPressed: controller.session == null
                  ? null
                  : controller.moveNext,
              icon: const Icon(Icons.skip_next),
              label: const Text('Next'),
            ),
            const Spacer(),
            TextButton.icon(
              onPressed: controller.session == null
                  ? null
                  : controller.closeSession,
              icon: const Icon(Icons.close),
              label: const Text('Close Session'),
            ),
          ],
        ),
      ),
    );
  }
}

class _SessionCard extends StatelessWidget {
  const _SessionCard({required this.session, required this.target});

  final SessionStateDto? session;
  final PlaybackTargetDto? target;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: session == null
            ? const Text(
                'No active session. Open a folder from Library to begin playback.',
              )
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    session!.folderName,
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 8),
                  Text('Current item: ${session!.currentItemId}'),
                  Text('Playlist count: ${session!.playlistCount}'),
                  Text('Saved progress: ${session!.savedProgressMs} ms'),
                  if (target != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      'Playback target',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                    const SizedBox(height: 4),
                    Text(target!.title),
                    Text(
                      target!.filePath,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ],
              ),
      ),
    );
  }
}

class _PlaylistCard extends StatelessWidget {
  const _PlaylistCard({
    required this.playlist,
    required this.currentItemId,
    required this.onSelect,
  });

  final PlaylistDto? playlist;
  final String? currentItemId;
  final Future<void> Function(String itemId) onSelect;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: playlist == null
            ? const Center(
                child: Text(
                  'Playlist will appear here after opening a session.',
                ),
              )
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Playlist',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 12),
                  Expanded(
                    child: ListView.separated(
                      itemCount: playlist!.items.length,
                      separatorBuilder: (_, index) => const Divider(height: 1),
                      itemBuilder: (context, index) {
                        final item = playlist!.items[index];
                        final isCurrent = item.itemId == currentItemId;
                        return ListTile(
                          dense: true,
                          title: Text(item.title),
                          subtitle: Text(
                            item.filePath,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          trailing: isCurrent
                              ? const Icon(Icons.play_arrow)
                              : null,
                          selected: isCurrent,
                          onTap: () => onSelect(item.itemId),
                        );
                      },
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}

class _VideoSurface extends StatefulWidget {
  const _VideoSurface({required this.playbackTarget});

  final PlaybackTargetDto? playbackTarget;

  @override
  State<_VideoSurface> createState() => _VideoSurfaceState();
}

class _VideoSurfaceState extends State<_VideoSurface> {
  late final Player _player;
  late final VideoController _videoController;
  String? _openedPath;

  @override
  void initState() {
    super.initState();
    _player = Player();
    _videoController = VideoController(_player);
    _openTargetIfNeeded();
  }

  @override
  void didUpdateWidget(covariant _VideoSurface oldWidget) {
    super.didUpdateWidget(oldWidget);
    _openTargetIfNeeded();
  }

  Future<void> _openTargetIfNeeded() async {
    final target = widget.playbackTarget;
    if (target == null || target.filePath == _openedPath) {
      return;
    }

    _openedPath = target.filePath;
    await _player.open(Media(target.filePath), play: true);
    if (target.startPositionMs > 0) {
      await _player.seek(Duration(milliseconds: target.startPositionMs));
    }
  }

  @override
  void dispose() {
    _player.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.playbackTarget == null) {
      return const Card(
        child: Center(
          child: Text(
            'Open a folder and choose an item to start media_kit playback.',
          ),
        ),
      );
    }

    return ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: ColoredBox(
        color: Colors.black,
        child: Video(
          controller: _videoController,
          controls: AdaptiveVideoControls,
        ),
      ),
    );
  }
}
