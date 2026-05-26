import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/window/app_page.dart';
import 'package:flutter/foundation.dart';

class PlayerLaunchCoordinator {
  const PlayerLaunchCoordinator({
    required this.controller,
    required this.showPage,
  });

  final AppController controller;
  final ValueChanged<AppPage> showPage;

  Future<String?> openFolderAndPlay(String folderId) async {
    controller.selectFolder(folderId);
    final error = await controller.openLibraryFolder(folderId);
    if (error != null) {
      return error;
    }

    showPage(AppPage.player);
    await controller.player.play();
    return null;
  }
}
