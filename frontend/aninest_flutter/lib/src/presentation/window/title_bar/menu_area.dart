import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_controller.dart';
import 'package:aninest_flutter/src/presentation/feedback/app_feedback_models.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/menu_area/backend_connection_dialog.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/menu_area/library_folder_menu_actions.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class MenuArea extends StatelessWidget {
  const MenuArea({
    super.key,
    required this.controller,
    required this.feedbackController,
  });

  final AppController controller;
  final AppFeedbackController feedbackController;

  Future<void> _handleBackendConnection(BuildContext context) async {
    final l10n = AppLocalizations.of(context);
    final updatedBaseUrl = await showDialog<String>(
      context: context,
      builder: (context) => BackendConnectionDialog(controller: controller),
    );
    if (updatedBaseUrl == null) {
      return;
    }

    feedbackController.publish(
      AppFeedbackRequest(
        kind: AppFeedbackKind.toastInfo,
        title: l10n.backendConnectionUpdatedTitle,
        message: l10n.backendConnectionUpdatedMessage(updatedBaseUrl),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final folderActions = LibraryFolderMenuActions(
      controller: controller,
      feedbackController: feedbackController,
    );

    return AnimatedBuilder(
      animation: controller,
      builder: (context, _) => Menubar(
        children: [
          MenuButton(
            subMenu: [
              MenuButton(
                leading: const Icon(BootstrapIcons.folder2Open),
                onPressed: (context) {
                  unawaited(folderActions.handleAddFolder(context));
                },
                child: Text(l10n.menuAddFolder),
              ),
              MenuButton(
                leading: const Icon(BootstrapIcons.folderPlus),
                onPressed: (context) {
                  unawaited(folderActions.handleScanFolder(context));
                },
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
                leading: const Icon(LucideIcons.server),
                onPressed: (context) {
                  unawaited(_handleBackendConnection(context));
                },
                child: Text(l10n.menuBackendConnection),
              ),
            ],
            child: Text(l10n.menuSettings),
          ),
        ],
      ),
    );
  }
}
