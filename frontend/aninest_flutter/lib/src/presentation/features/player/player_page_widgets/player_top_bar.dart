import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_region_placeholder.dart';

class PlayerTopBar extends StatelessWidget {
  const PlayerTopBar({super.key});

  @override
  Widget build(BuildContext context) {
    return const PlayerRegionPlaceholder(
      title: 'Top Bar',
      color: Color(0xFF1D4ED8),
    );
  }
}
