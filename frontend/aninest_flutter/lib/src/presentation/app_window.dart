import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:aninest_flutter/src/presentation/window/content_area.dart';
import 'package:aninest_flutter/src/presentation/window/sidebar.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class AppWindow extends StatefulWidget {
  const AppWindow({super.key, required this.controller});

  final AppController controller;

  @override
  State<AppWindow> createState() => _AppWindowState();
}

class _AppWindowState extends State<AppWindow> {
  late final WindowFrameController _windowFrameController;

  @override
  void initState() {
    super.initState();
    _windowFrameController = WindowFrameController(const WindowService());
    if (AppPlatform.isDesktop) {
      _windowFrameController.attach();
    }
  }

  @override
  void dispose() {
    _windowFrameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      backgroundColor: colorScheme.background,
      child: Column(
        children: <Widget>[
          if (AppPlatform.isDesktop)
            TitleBar(
              controller: _windowFrameController,
              appController: widget.controller,
            ),
          Container(height: 1, color: colorScheme.border),
          Expanded(
            child: Row(
              children: <Widget>[
                Sidebar(),
                Expanded(child: Align(
                  alignment: Alignment.center,
                  child: ContentArea(controller: widget.controller),
                )),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
