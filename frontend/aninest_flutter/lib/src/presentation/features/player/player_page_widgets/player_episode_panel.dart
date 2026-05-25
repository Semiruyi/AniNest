import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_region_placeholder.dart';

class PlayerEpisodePanel extends StatelessWidget {
  const PlayerEpisodePanel({super.key});

  @override
  Widget build(BuildContext context) {
    return const PlayerRegionPlaceholder(
      title: 'Episode Panel',
      color: Color(0xFFB45309),
    );
  }
}
