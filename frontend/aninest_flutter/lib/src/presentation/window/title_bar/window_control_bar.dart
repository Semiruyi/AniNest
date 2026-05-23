import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class WindowControlBar extends StatelessWidget {
  const WindowControlBar({super.key, required this.controller});

  final WindowFrameController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (context, child) {
        return Row(
          children: <Widget>[
            _WindowControlButton(
              icon: Icons.remove,
              tooltip: 'Minimize',
              onPressed: controller.minimize,
            ),
            _WindowControlButton(
              icon: controller.isMaximized
                  ? Icons.filter_none
                  : Icons.crop_square,
              tooltip: controller.isMaximized ? 'Restore' : 'Maximize',
              onPressed: controller.toggleMaximize,
            ),
            _WindowControlButton(
              icon: Icons.close,
              tooltip: 'Close',
              destructive: true,
              onPressed: controller.close,
            ),
          ],
        );
      },
    );
  }
}

class _WindowControlButton extends StatelessWidget {
  const _WindowControlButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    this.destructive = false,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;
  final bool destructive;

  @override
  Widget build(BuildContext context) {
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
}
