import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'control_bar_widgets/player_control_bar_button_row.dart';
import 'control_bar_widgets/player_control_bar_progress_section.dart';

class PlayerControlBar extends StatelessWidget {
  const PlayerControlBar({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
      ),
      padding: const EdgeInsets.all(4),
      child: const Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Gap(4),
          SizedBox(height: 18, child: PlayerControlBarProgressSection()),
          SizedBox(height: 4),
          Expanded(child: PlayerControlBarButtonRow()),
        ],
      ),
    );
  }
}
