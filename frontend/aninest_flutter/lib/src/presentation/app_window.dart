import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/window/window_frame_controller.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_controller.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_models.dart';
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
  late final AppFeedbackController _feedbackController;
  bool _isPresentingFeedback = false;

  @override
  void initState() {
    super.initState();
    _windowFrameController = WindowFrameController(const WindowService());
    _feedbackController = AppFeedbackController();
    _feedbackController.addListener(_handleFeedbackChanged);
    if (AppPlatform.isDesktop) {
      _windowFrameController.attach();
    }
  }

  @override
  void dispose() {
    _feedbackController.removeListener(_handleFeedbackChanged);
    _feedbackController.dispose();
    _windowFrameController.dispose();
    super.dispose();
  }

  Future<void> _handleFeedbackChanged() async {
    if (_isPresentingFeedback) {
      return;
    }

    _isPresentingFeedback = true;
    try {
      while (mounted) {
        final request = _feedbackController.takeNext();
        if (request == null) {
          break;
        }

        await _presentFeedback(request);
      }
    } finally {
      _isPresentingFeedback = false;
    }
  }

  Future<void> _presentFeedback(AppFeedbackRequest request) async {
    switch (request.kind) {
      case AppFeedbackKind.toastInfo:
        showToast(
          context: context,
          location: ToastLocation.bottomRight,
          builder: (context, overlay) => _buildToastCard(
            icon: const Icon(RadixIcons.infoCircled),
            title: request.title,
            message: request.message,
            overlay: overlay,
          ),
        );
        return;
      case AppFeedbackKind.dialogError:
        await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text(request.title),
            content: Text(request.message),
            actions: [
              PrimaryButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Close'),
              ),
            ],
          ),
        );
        return;
    }
  }

  Widget _buildToastCard({
    required Widget icon,
    required String title,
    required String message,
    required ToastOverlay overlay,
  }) {
    return SurfaceCard(
      child: Basic(
        title: Text(title),
        subtitle: Text(message),
        trailing: PrimaryButton(
            size: ButtonSize.small,
            onPressed: () {
              // Close the toast programmatically when clicking Undo.
              overlay.close();
            },
            child: const Text('OK')),
        trailingAlignment: Alignment.center,
      ),
    );
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
              feedbackController: _feedbackController,
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
