import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_episode_list_item.dart';
import 'player_episode_panel_empty_state.dart';
import 'player_episode_panel_header.dart';

class PlayerEpisodePanelFrame extends StatelessWidget {
  const PlayerEpisodePanelFrame({
    super.key,
    required this.playlist,
    required this.selectedItemId,
    required this.currentPlaybackItemId,
    required this.currentPlaybackStartPositionMs,
    required this.currentPlaybackProgressFraction,
    required this.scrollController,
    required this.onItemPressed,
  });

  static const double itemExtent = 70;
  static const double minItemWidth = 140;
  static const double maxItemWidth = 220;
  static const double gridSpacing = 8;
  static const EdgeInsets gridPadding = EdgeInsets.fromLTRB(10, 4, 10, 12);

  final PlaylistDto? playlist;
  final String? selectedItemId;
  final String? currentPlaybackItemId;
  final int? currentPlaybackStartPositionMs;
  final double? currentPlaybackProgressFraction;
  final ScrollController scrollController;
  final ValueChanged<String> onItemPressed;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final items = playlist?.items ?? const <PlaylistItemDto>[];

    return Container(
      decoration: BoxDecoration(
        color: colorScheme.background,
        border: Border(left: BorderSide(color: colorScheme.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          PlayerEpisodePanelHeader(
            title: playlist?.folderName ?? 'Episodes',
            currentIndex: _selectedIndex(items, selectedItemId),
            totalCount: items.length,
          ),
          Expanded(
            child: items.isEmpty
                ? const PlayerEpisodePanelEmptyState()
                : LayoutBuilder(
                    builder:
                        (BuildContext context, BoxConstraints constraints) {
                          final crossAxisCount = _computeCrossAxisCount(
                            availableWidth: constraints.maxWidth,
                            itemCount: items.length,
                          );

                          return GridView.builder(
                            controller: scrollController,
                            padding: gridPadding,
                            gridDelegate:
                                SliverGridDelegateWithFixedCrossAxisCount(
                                  crossAxisCount: crossAxisCount,
                                  mainAxisExtent: itemExtent,
                                  crossAxisSpacing: gridSpacing,
                                  mainAxisSpacing: gridSpacing,
                                ),
                            itemCount: items.length,
                            itemBuilder: (BuildContext context, int index) {
                              final item = items[index];
                              return PlayerEpisodeListItem(
                                item: item,
                                isSelected: item.itemId == selectedItemId,
                                progressFraction: _progressFractionForItem(
                                  item,
                                ),
                                onPressed: () => onItemPressed(item.itemId),
                              );
                            },
                          );
                        },
                  ),
          ),
        ],
      ),
    );
  }

  int _computeCrossAxisCount({
    required double availableWidth,
    required int itemCount,
  }) {
    final contentWidth = availableWidth - gridPadding.left - gridPadding.right;
    if (contentWidth <= 0 || itemCount <= 1) {
      return 1;
    }

    final minColumns =
        ((contentWidth + gridSpacing) / (maxItemWidth + gridSpacing))
            .ceil()
            .clamp(1, itemCount);
    final maxColumns =
        ((contentWidth + gridSpacing) / (minItemWidth + gridSpacing))
            .floor()
            .clamp(1, itemCount);

    return maxColumns >= minColumns ? maxColumns : minColumns;
  }

  double _progressFractionForItem(PlaylistItemDto item) {
    if (item.itemId == currentPlaybackItemId) {
      final runtimeFraction = currentPlaybackProgressFraction;
      if (runtimeFraction != null) {
        return runtimeFraction;
      }

      final startPositionMs = currentPlaybackStartPositionMs;
      if (startPositionMs != null) {
        if (startPositionMs <= 0) {
          return 0;
        }
        if (item.durationMs > 0) {
          return (startPositionMs / item.durationMs).clamp(0.0, 1.0);
        }
      }
    }

    if (!item.hasSavedProgress || item.durationMs <= 0) {
      return item.isPlayed ? 1 : 0;
    }

    return (item.savedProgressMs / item.durationMs).clamp(0.0, 1.0);
  }

  int? _selectedIndex(List<PlaylistItemDto> items, String? selectedItemId) {
    if (selectedItemId == null) {
      return null;
    }

    final index = items.indexWhere(
      (PlaylistItemDto item) => item.itemId == selectedItemId,
    );
    return index < 0 ? null : index;
  }
}
