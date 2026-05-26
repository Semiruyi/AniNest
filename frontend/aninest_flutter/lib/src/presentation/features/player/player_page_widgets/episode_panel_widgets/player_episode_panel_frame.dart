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
    required this.scrollController,
    required this.onItemPressed,
  });

  static const double itemExtent = 72;

  final PlaylistDto? playlist;
  final String? selectedItemId;
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
                : ListView.builder(
                    controller: scrollController,
                    itemExtent: itemExtent,
                    padding: const EdgeInsets.fromLTRB(10, 4, 10, 12),
                    itemCount: items.length,
                    itemBuilder: (BuildContext context, int index) {
                      final item = items[index];
                      return PlayerEpisodeListItem(
                        item: item,
                        isSelected: item.itemId == selectedItemId,
                        onPressed: () => onItemPressed(item.itemId),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
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
