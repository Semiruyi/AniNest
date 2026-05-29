import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'control_bar_widgets/player_control_bar_button_row.dart';
import 'control_bar_widgets/player_control_bar_progress_section.dart';
import 'control_bar_widgets/player_control_bar_state_controller.dart';

class PlayerControlBar extends StatefulWidget {
  const PlayerControlBar({
    super.key,
    required this.controller,
    required this.onToggleFullscreen,
    this.isFullscreen = false,
  });

  final AppController controller;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  @override
  State<PlayerControlBar> createState() => _PlayerControlBarState();
}

class _PlayerControlBarState extends State<PlayerControlBar> {
  late final PlayerControlBarStateController _stateController;

  @override
  void initState() {
    super.initState();
    _stateController = PlayerControlBarStateController(
      appController: widget.controller,
    );
  }

  @override
  void dispose() {
    _stateController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(borderRadius: BorderRadius.circular(8)),
      padding: const EdgeInsets.all(4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const Gap(4),
          SizedBox(
            height: 18,
            child: PlayerControlBarProgressSection(controller: widget.controller),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: PlayerControlBarButtonRow(
              controller: _stateController,
              isFullscreen: widget.isFullscreen,
              onToggleFullscreen: widget.onToggleFullscreen,
            ),
          ),
        ],
      ),
    );
  }
}
