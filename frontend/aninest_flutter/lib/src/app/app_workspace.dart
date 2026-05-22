import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/core/widgets/workspace_frame.dart';
import 'package:aninest_flutter/src/features/library/presentation/library_page.dart';
import 'package:aninest_flutter/src/features/metadata/presentation/metadata_page.dart';
import 'package:aninest_flutter/src/features/player/presentation/player_page.dart';
import 'package:aninest_flutter/src/features/settings/presentation/settings_page.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class AppWorkspace extends StatelessWidget {
  const AppWorkspace({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    return WorkspaceFrame(
      sidebar: LibraryPage(controller: controller.library),
      content: PlayerPage(controller: controller.player),
      inspector: MetadataPage(controller: controller.metadata),
      bottomPane: SettingsPage(controller: controller.settings),
    );
  }
}
