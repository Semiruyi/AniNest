import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page.dart';
import 'package:aninest_flutter/src/presentation/features/player/player_page.dart';
import 'package:aninest_flutter/src/presentation/keyboard/player_focus_controller.dart';
import 'package:aninest_flutter/src/presentation/window/animated_page_stack.dart';
import 'package:aninest_flutter/src/presentation/window/app_page.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ContentArea extends StatefulWidget {
  const ContentArea({
    super.key,
    required this.controller,
    required this.currentPage,
    required this.onOpenFolderForPlayback,
    required this.isPlayerFullscreen,
    required this.onTogglePlayerFullscreen,
    this.inspectorWidth = 320,
    this.bottomPaneHeight = 220,
  });

  final AppController controller;
  final AppPage currentPage;
  final Future<String?> Function(String folderId) onOpenFolderForPlayback;
  final bool isPlayerFullscreen;
  final VoidCallback onTogglePlayerFullscreen;
  final double inspectorWidth;
  final double bottomPaneHeight;

  @override
  State<ContentArea> createState() => _ContentAreaState();
}

class _ContentAreaState extends State<ContentArea> {
  late final PlayerFocusController _playerFocusController;
  late AppPage _presentedPage;

  @override
  void initState() {
    super.initState();
    _playerFocusController = PlayerFocusController();
    _presentedPage = widget.currentPage;
  }

  @override
  void dispose() {
    _playerFocusController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final pages = <Widget>[
      LibraryPage(
        controller: widget.controller,
        onOpenFolderForPlayback: widget.onOpenFolderForPlayback,
      ),
      PlayerPage(
        controller: widget.controller.player,
        focusController: _playerFocusController,
        isSelected: widget.currentPage == AppPage.player,
        isPresented: _presentedPage == AppPage.player,
        isFullscreen: widget.isPlayerFullscreen,
        onToggleFullscreen: widget.onTogglePlayerFullscreen,
      ),
    ];

    return SizedBox.expand(
      child: AnimatedPageStack(
        index: widget.currentPage.index,
        duration: const Duration(milliseconds: 260),
        curve: Curves.easeOutCubic,
        onPresentedIndexChanged: _handlePresentedIndexChanged,
        children: pages,
        animationBuilder:
            (BuildContext context, Animation<double> animation, Widget? child) {
              return FadeTransition(opacity: animation, child: child);
            },
      ),
    );
  }

  void _handlePresentedIndexChanged(int index) {
    final presentedPage = AppPage.values[index];
    if (_presentedPage == presentedPage) {
      return;
    }

    setState(() {
      _presentedPage = presentedPage;
    });
  }
}
