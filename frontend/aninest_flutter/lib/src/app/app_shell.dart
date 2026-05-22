import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_workspace.dart';
import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/widgets/desktop_title_bar.dart';
import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:flutter/material.dart';

class AppShell extends StatefulWidget {
  const AppShell({super.key, required this.controller});

  final AppController controller;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
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
    return Scaffold(
      body: Column(
        children: <Widget>[
          if (AppPlatform.isDesktop)
            DesktopTitleBar(controller: _windowFrameController),
          Expanded(
            child: AppWorkspace(controller: widget.controller),
          ),
        ],
      ),
    );
  }
}
