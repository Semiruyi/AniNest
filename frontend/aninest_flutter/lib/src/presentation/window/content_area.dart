import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page.dart';
import 'package:aninest_flutter/src/presentation/features/player/player_page.dart';
import 'package:aninest_flutter/src/presentation/window/app_page.dart';
import 'package:easy_animated_indexed_stack/easy_animated_indexed_stack.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ContentArea extends StatelessWidget {
  const ContentArea({
    super.key,
    required this.controller,
    required this.currentPage,
    required this.onOpenFolderForPlayback,
    this.inspectorWidth = 320,
    this.bottomPaneHeight = 220,
  });

  final AppController controller;
  final AppPage currentPage;
  final Future<String?> Function(String folderId) onOpenFolderForPlayback;
  final double inspectorWidth;
  final double bottomPaneHeight;

  @override
  Widget build(BuildContext context) {
    final pages = <Widget>[
      LibraryPage(
        controller: controller,
        onOpenFolderForPlayback: onOpenFolderForPlayback,
      ),
      PlayerPage(
        controller: controller.player,
        isActive: currentPage == AppPage.player,
      ),
    ];

    return SizedBox.expand(
      child: EasyAnimatedIndexedStack(
        index: currentPage.index,
        duration: const Duration(milliseconds: 260),
        curve: Curves.easeOutCubic,
        children: pages,
        animationBuilder:
            (BuildContext context, Animation<double> animation, Widget? child) {
              return FadeTransition(opacity: animation, child: child);
            },
      ),
    );
  }
}
