import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/features/metadata/metadata_page.dart';
import 'package:aninest_flutter/src/presentation/features/player/player_page.dart';
import 'package:aninest_flutter/src/presentation/features/settings/settings_page.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class Content extends StatelessWidget {
  const Content({
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
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      children: <Widget>[
        Expanded(
          child: Row(
            children: <Widget>[
              Expanded(
                child: DecoratedBox(
                  decoration: BoxDecoration(color: colorScheme.background),
                  child: PlayerPage(controller: controller.player),
                ),
              ),
              SizedBox(
                width: inspectorWidth,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: colorScheme.card,
                    border: Border(left: BorderSide(color: colorScheme.border)),
                  ),
                  child: MetadataPage(controller: controller.metadata),
                ),
              ),
            ],
          ),
        ),
        SizedBox(
          height: bottomPaneHeight,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: colorScheme.card,
              border: Border(top: BorderSide(color: colorScheme.border)),
            ),
            child: SettingsPage(controller: controller.settings),
          ),
        ),
      ],
    );
  }
}
