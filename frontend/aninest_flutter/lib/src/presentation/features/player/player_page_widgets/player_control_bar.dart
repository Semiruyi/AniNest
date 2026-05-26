import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'control_bar_widgets/player_control_bar_button_row.dart';
import 'control_bar_widgets/player_control_bar_progress_section.dart';

class PlayerControlBar extends StatelessWidget {
  const PlayerControlBar({
    super.key,
    required this.controller,
  });

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(8),
      ),
      padding: const EdgeInsets.all(4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const Gap(4),
          SizedBox(
            height: 18,
            child: PlayerControlBarProgressSection(controller: controller),
          ),
          const SizedBox(height: 4),
          Expanded(child: PlayerControlBarButtonRow(controller: controller)),
        ],
      ),
    );
  }
}
