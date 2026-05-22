import 'package:aninest_flutter/src/core/widgets/title_bar_button.dart';
import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:flutter/material.dart';
import 'package:window_manager/window_manager.dart';

class DesktopTitleBar extends StatelessWidget {
  const DesktopTitleBar({
    super.key,
    required this.controller,
    this.title = 'AniNest',
  });

  final WindowFrameController controller;
  final String title;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Material(
      color: theme.colorScheme.surface,
      child: SizedBox(
        height: 44,
        child: AnimatedBuilder(
          animation: controller,
          builder: (BuildContext context, Widget? child) {
            return Row(
              children: <Widget>[
                Expanded(
                  child: DragToMoveArea(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                      child: Align(
                        alignment: Alignment.centerLeft,
                        child: Text(
                          title,
                          style: theme.textTheme.titleSmall,
                        ),
                      ),
                    ),
                  ),
                ),
                TitleBarButton(
                  icon: Icons.remove,
                  tooltip: 'Minimize',
                  onPressed: controller.minimize,
                ),
                TitleBarButton(
                  icon: controller.isMaximized
                      ? Icons.filter_none
                      : Icons.crop_square,
                  tooltip: controller.isMaximized ? 'Restore' : 'Maximize',
                  onPressed: controller.toggleMaximize,
                ),
                TitleBarButton(
                  icon: Icons.close,
                  tooltip: 'Close',
                  onPressed: controller.close,
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
