import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/storage/library_page_preferences.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_content_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_inspector_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_navigation_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_status_bar.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_toolbar.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryPage extends StatefulWidget {
  const LibraryPage({
    super.key,
    required this.controller,
    required this.onOpenFolderForPlayback,
  });

  final AppController controller;
  final Future<String?> Function(String folderId) onOpenFolderForPlayback;

  @override
  State<LibraryPage> createState() => _LibraryPageState();
}

class _LibraryPageState extends State<LibraryPage> {
  late final AbsoluteResizablePaneController _leftPaneController;
  late final AbsoluteResizablePaneController _rightPaneController;

  @override
  void initState() {
    super.initState();
    _leftPaneController = AbsoluteResizablePaneController(
      kLibraryLeftPaneInitialSize,
    );
    _rightPaneController = AbsoluteResizablePaneController(
      kLibraryRightPaneInitialSize,
    );
    unawaited(_restorePaneWidths());
  }

  @override
  void dispose() {
    _leftPaneController.dispose();
    _rightPaneController.dispose();
    super.dispose();
  }

  Future<void> _restorePaneWidths() async {
    final savedPreferences = await widget.controller.appPreferences
        .loadLibraryPagePreferences();
    if (!mounted || savedPreferences == null) {
      return;
    }

    _leftPaneController.size = _clampWidth(
      savedPreferences.leftPaneWidth,
      min: kLibraryLeftPaneMinSize,
      max: kLibraryLeftPaneMaxSize,
      fallback: kLibraryLeftPaneInitialSize,
    );
    _rightPaneController.size = _clampWidth(
      savedPreferences.rightPaneWidth,
      min: kLibraryRightPaneMinSize,
      max: kLibraryRightPaneMaxSize,
      fallback: kLibraryRightPaneInitialSize,
    );
  }

  Future<void> _persistPaneWidths() {
    return widget.controller.appPreferences.saveLibraryPagePreferences(
      LibraryPagePreferences(
        leftPaneWidth: _leftPaneController.value,
        rightPaneWidth: _rightPaneController.value,
      ),
    );
  }

  double _clampWidth(
    double? value, {
    required double min,
    required double max,
    required double fallback,
  }) {
    if (value == null || value.isNaN || !value.isFinite) {
      return fallback;
    }

    return value.clamp(min, max);
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      child: Column(
        children: <Widget>[
          const LibraryToolbar(),
          Expanded(
            child: ResizablePanel.horizontal(
              draggerBuilder: ResizablePanel.defaultDraggerBuilder,
              draggerThickness: 12,
              children: <ResizablePane>[
                ResizablePane.controlled(
                  controller: _leftPaneController,
                  minSize: kLibraryLeftPaneMinSize,
                  maxSize: kLibraryLeftPaneMaxSize,
                  onSizeChangeEnd: (_) {
                    unawaited(_persistPaneWidths());
                  },
                  child: const LibraryNavigationPane(),
                ),
                ResizablePane.flex(
                  minSize: kLibraryContentPaneMinSize,
                  child: LibraryContentPane(
                    controller: widget.controller,
                    onOpenFolderForPlayback: widget.onOpenFolderForPlayback,
                  ),
                ),
                ResizablePane.controlled(
                  controller: _rightPaneController,
                  minSize: kLibraryRightPaneMinSize,
                  maxSize: kLibraryRightPaneMaxSize,
                  onSizeChangeEnd: (_) {
                    unawaited(_persistPaneWidths());
                  },
                  child: LibraryInspectorPane(
                    libraryController: widget.controller.library,
                    metadataController: widget.controller.metadata,
                  ),
                ),
              ],
            ),
          ),
          LibraryStatusBar(controller: widget.controller.library),
        ],
      ),
    );
  }
}
