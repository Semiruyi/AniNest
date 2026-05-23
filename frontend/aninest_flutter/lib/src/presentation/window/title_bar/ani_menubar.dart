import 'package:flutter/services.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class AniMenubar extends StatefulWidget {
  const AniMenubar({super.key});

  @override
  State<AniMenubar> createState() => _AniMenubarState();
}

class _AniMenubarState extends State<AniMenubar> {
  int _selectedLanguage = 1;

  @override
  Widget build(BuildContext context) {
    return Menubar(
      children: [
        const MenuButton(
          subMenu: [
            MenuButton(
              leading: Icon(BootstrapIcons.folder2Open),
              child: Text('Add Folder'),
            ),
            MenuButton(
              leading: Icon(BootstrapIcons.folderPlus),
              child: Text('Scan Folder'),
            ),
          ],
          child: Text('File'),
        ),

        MenuButton(
          subMenu: [
            MenuButton(
              subMenu: [
                MenuRadioGroup<int>(
                value: _selectedLanguage,
                onChanged: (context, value) {
                  setState(() {
                    _selectedLanguage = value;
                  });
                },
                children: const [
                  MenuRadio<int>(
                    value: 0,
                    autoClose: false,
                    child: Text('简体中文'),
                  ),
                  MenuRadio<int>(
                    value: 1,
                    autoClose: false,
                    child: Text('English'),
                  ),
                ],
              ),
              ],
              child: Text('Language')
            ),
          ],
          child: const Text('Settings'),
        ),
      ],
    );
  }
}
