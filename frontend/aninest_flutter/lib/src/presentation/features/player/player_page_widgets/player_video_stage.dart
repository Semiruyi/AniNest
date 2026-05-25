import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_region_placeholder.dart';

class PlayerVideoStage extends StatelessWidget {
  const PlayerVideoStage({super.key});

  @override
  Widget build(BuildContext context) {
    return const PlayerRegionPlaceholder(
      title: 'Video Stage',
      color: Color(0xFF7C3AED),
    );
  }
}
