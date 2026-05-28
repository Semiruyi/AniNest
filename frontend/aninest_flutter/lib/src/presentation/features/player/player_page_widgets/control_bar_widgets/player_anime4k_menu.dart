import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerAnime4kMenu extends StatelessWidget {
  const PlayerAnime4kMenu({
    super.key,
    required this.selectedMode,
    required this.onModeSelected,
    required this.onDismissRequested,
  });

  final PlayerAnime4kMode selectedMode;
  final ValueChanged<PlayerAnime4kMode> onModeSelected;
  final VoidCallback onDismissRequested;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(minWidth: 180, maxWidth: 220),
      child: MenuGroup(
        itemPadding: EdgeInsets.zero,
        direction: Axis.vertical,
        onDismissed: onDismissRequested,
        builder: (BuildContext context, List<Widget> children) {
          return MenuPopup(children: children);
        },
        children: <MenuItem>[
          MenuRadioGroup<PlayerAnime4kMode>(
            value: selectedMode,
            onChanged: (BuildContext context, PlayerAnime4kMode value) {
              onModeSelected(value);
              onDismissRequested();
            },
            children: PlayerAnime4kMode.values
                .map(
                  (PlayerAnime4kMode mode) => MenuRadio<PlayerAnime4kMode>(
                    value: mode,
                    child: Text(mode.label),
                  ),
                )
                .toList(),
          ),
        ],
      ),
    );
  }
}
