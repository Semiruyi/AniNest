import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class AniMenubar extends StatelessWidget {
  const AniMenubar({super.key, required this.controller});

  final AppController controller;

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
