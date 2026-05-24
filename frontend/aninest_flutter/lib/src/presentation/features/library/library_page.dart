import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_content_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_inspector_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_navigation_pane.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_status_bar.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_toolbar.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryPage extends StatelessWidget {
  const LibraryPage({super.key, required this.controller});

  final LibraryController controller;

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
              children: const <ResizablePane>[
                ResizablePane(
                  initialSize: kLibraryLeftPaneInitialSize,
                  minSize: kLibraryLeftPaneMinSize,
                  maxSize: kLibraryLeftPaneMaxSize,
                  child: LibraryNavigationPane(),
                ),
                ResizablePane.flex(
                  minSize: kLibraryContentPaneMinSize,
                  child: LibraryContentPane(),
                ),
                ResizablePane(
                  initialSize: kLibraryRightPaneInitialSize,
                  minSize: kLibraryRightPaneMinSize,
                  maxSize: kLibraryRightPaneMaxSize,
                  child: LibraryInspectorPane(),
                ),
              ],
            ),
          ),
          const LibraryStatusBar(),
        ],
      ),
    );
  }
}
