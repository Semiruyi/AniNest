import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/core/platform/directory_picker.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_controller.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class AniMenubar extends StatelessWidget {
  const AniMenubar({
    super.key,
    required this.controller,
    required this.feedbackController,
    this.directoryPicker = const DirectoryPicker(),
  });

  final AppController controller;
  final AppFeedbackController feedbackController;
  final DirectoryPicker directoryPicker;

  Future<void> _handleAddFolder() async {
    final path = await directoryPicker.pickDirectory();
    if (path == null || path.isEmpty) {
      return;
    }

    await controller.addFolder(path);
  }

  void _showTestInfoToast() {
    feedbackController.publish(
      const AppFeedbackRequest(
        kind: AppFeedbackKind.toastInfo,
        title: 'Test Info',
        message: 'This is an informational toast from the window feedback layer.',
      ),
    );
  }

  void _showTestErrorDialog() {
    feedbackController.publish(
      const AppFeedbackRequest(
        kind: AppFeedbackKind.dialogError,
        title: 'Test Error',
        message: 'This is a dialog-based error message from the window feedback layer.',
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return AnimatedBuilder(
      animation: controller,
      builder: (context, _) => Menubar(
        children: [
          MenuButton(
            subMenu: [
              MenuButton(
                leading: const Icon(BootstrapIcons.folder2Open),
                onPressed: (context) {
                  unawaited(_handleAddFolder());
                },
                child: Text(l10n.menuAddFolder),
              ),
              MenuButton(
                leading: const Icon(BootstrapIcons.folderPlus),
                child: Text(l10n.menuScanFolder),
              ),
            ],
            child: Text(l10n.menuFile),
          ),
          MenuButton(
            subMenu: [
              MenuButton(
                subMenu: [
                  MenuRadioGroup<AppLocaleOption>(
                    value: controller.locale,
                    onChanged: (context, value) async {
                      await controller.saveLocale(value);
                    },
                    children: [
                      MenuRadio<AppLocaleOption>(
                        value: AppLocaleOption.simplifiedChinese,
                        autoClose: false,
                        child: Text(l10n.languageSimplifiedChinese),
                      ),
                      MenuRadio<AppLocaleOption>(
                        value: AppLocaleOption.english,
                        autoClose: false,
                        child: Text(l10n.languageEnglish),
                      ),
                    ],
                  ),
                ],
                child: Text(l10n.menuLanguage),
              ),
              const MenuDivider(),
              MenuButton(
                leading: const Icon(RadixIcons.infoCircled),
                onPressed: (context) => _showTestInfoToast(),
                child: const Text('Test Info Toast'),
              ),
              MenuButton(
                leading: const Icon(RadixIcons.exclamationTriangle),
                onPressed: (context) => _showTestErrorDialog(),
                child: const Text('Test Error Dialog'),
              ),
            ],
            child: Text(l10n.menuSettings),
          ),
        ],
      ),
    );
  }
}
