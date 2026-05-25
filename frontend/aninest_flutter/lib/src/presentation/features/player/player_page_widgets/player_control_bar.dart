import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'control_bar_widgets/player_control_bar_button_row.dart';
import 'control_bar_widgets/player_control_bar_progress_section.dart';

class PlayerControlBar extends StatelessWidget {
  const PlayerControlBar({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFBE123C).withValues(alpha: 0.92),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: colorScheme.border.withValues(alpha: 0.5)),
      ),
      padding: const EdgeInsets.all(4),
      child: const Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SizedBox(height: 18, child: PlayerControlBarProgressSection()),
          SizedBox(height: 4),
          Expanded(child: PlayerControlBarButtonRow()),
        ],
      ),
    );
  }
}
