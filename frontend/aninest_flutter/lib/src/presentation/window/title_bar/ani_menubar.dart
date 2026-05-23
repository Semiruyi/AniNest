import 'dart:async';
import 'dart:io';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
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

  Future<void> _handleAddFolder(BuildContext context) async {
    final l10n = AppLocalizations.of(context);

    try {
      final path = await directoryPicker.pickDirectory();
      if (path == null || path.isEmpty) {
        return;
      }

      final result = await controller.addFolder(path);
      if (result == null) {
        AppLogger.warning(
          'AniMenubar.AddFolder',
          'Received null addFolder result.',
        );
        return;
      }

      if (result.isAdded) {
        return;
      }

      final folderName = _folderDisplayName(path, result.folder?.name);
      if (result.isAlreadyExists) {
        feedbackController.publish(
          AppFeedbackRequest(
            kind: AppFeedbackKind.toastInfo,
            title: l10n.addFolderAlreadyAddedTitle,
            message: l10n.addFolderAlreadyAddedMessage(folderName),
          ),
        );
        return;
      }

      feedbackController.publish(
        AppFeedbackRequest(
          kind: AppFeedbackKind.dialogError,
          title: l10n.addFolderErrorTitle,
          message: result.message,
        ),
      );
    } catch (error, stackTrace) {
      AppLogger.error(
        'AniMenubar.AddFolder',
        'Unhandled exception while processing addFolder.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  String _folderDisplayName(String path, String? fallbackName) {
    if (fallbackName != null && fallbackName.isNotEmpty) {
      return fallbackName;
    }

    final name = path.split(Platform.pathSeparator).last;
    return name.isEmpty ? path : name;
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
                  unawaited(_handleAddFolder(context));
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
            ],
            child: Text(l10n.menuSettings),
          ),
        ],
      ),
    );
  }
}
