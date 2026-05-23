import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/features/metadata/metadata_page.dart';
import 'package:aninest_flutter/src/presentation/features/player/player_page.dart';
import 'package:aninest_flutter/src/presentation/features/settings/settings_page.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ContentArea extends StatelessWidget {
  const ContentArea({
    super.key,
    required this.controller,
    this.inspectorWidth = 320,
    this.bottomPaneHeight = 220,
  });

  final AppController controller;
  final double inspectorWidth;
  final double bottomPaneHeight;

  @override
  Widget build(BuildContext context) {

    return Text("content");
  }
}
