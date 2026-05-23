import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/drag_bar.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/menu_bar.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/window_control_bar.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class TitleBar extends StatelessWidget {
  const TitleBar({super.key, required this.controller, this.title = 'AniNest'});

  final WindowFrameController controller;
  final String title;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      height: 44,
      child: Row(
        children: <Widget>[
          MenuBar(title: title),
          const Expanded(child: DragBar()),
          WindowControlBar(controller: controller),
        ],
      ),
    );
  }
}
