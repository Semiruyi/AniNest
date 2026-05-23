import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerPage extends StatelessWidget {
  const PlayerPage({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(color: Color(0xFF15803D)),
      child: const SizedBox.expand(),
    );
  }
}
