import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';
import 'package:window_manager/window_manager.dart';

class TitleBar extends StatelessWidget {
  const TitleBar({super.key, required this.controller, this.title = 'AniNest'});

  final WindowFrameController controller;
  final String title;

  Widget _buildButton({
    required IconData icon,
    required String tooltip,
    required VoidCallback onPressed,
    bool destructive = false,
  }) {
    return SizedBox(
      width: 46,
      height: 44,
      child: Tooltip(
        tooltip: (context) => TooltipContainer(child: Text(tooltip)),
        child: destructive
            ? DestructiveButton(
                density: ButtonDensity.icon,
                onPressed: onPressed,
                child: Icon(icon),
              )
            : GhostButton(
                density: ButtonDensity.icon,
                onPressed: onPressed,
                child: Icon(icon),
              ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      height: 44,
      child: AnimatedBuilder(
        animation: controller,
        builder: (context, child) {
          return Row(
            children: <Widget>[
              Expanded(
                child: DragToMoveArea(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    child: Align(
                      alignment: Alignment.centerLeft,
                      child: Text(title),
                    ),
                  ),
                ),
              ),
              _buildButton(
                icon: Icons.remove,
                tooltip: 'Minimize',
                onPressed: controller.minimize,
              ),
              _buildButton(
                icon: controller.isMaximized
                    ? Icons.filter_none
                    : Icons.crop_square,
                tooltip: controller.isMaximized ? 'Restore' : 'Maximize',
                onPressed: controller.toggleMaximize,
              ),
              _buildButton(
                icon: Icons.close,
                tooltip: 'Close',
                destructive: true,
                onPressed: controller.close,
              ),
            ],
          );
        },
      ),
    );
  }
}
