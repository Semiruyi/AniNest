import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_controller.dart';
import 'package:aninest_flutter/src/presentation/window/window_layout.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/drag_bar.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/ani_menubar.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/window_control_bar.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class TitleBar extends StatelessWidget {
  const TitleBar({
    super.key,
    required this.controller,
    required this.appController,
    required this.feedbackController,
    this.title = 'Ani',
  });

  final WindowFrameController controller;
  final AppController appController;
  final AppFeedbackController feedbackController;
  final String title;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      height: kWindowTitleBarHeight,
      child: Row(
        children: <Widget>[
          SizedBox(
            width: kSidebarRailWidth,
            child: Center(
              child: FlutterLogo(size: 20,),
            ),
          ),
          AniMenubar(
            controller: appController,
            feedbackController: feedbackController,
          ),
          const Expanded(child: DragBar()),
          WindowControlBar(controller: controller),
        ],
      ),
    );
  }
}
