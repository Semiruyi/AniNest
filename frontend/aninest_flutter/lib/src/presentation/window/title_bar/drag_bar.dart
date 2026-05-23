import 'package:shadcn_flutter/shadcn_flutter.dart';
import 'package:window_manager/window_manager.dart';

class DragBar extends StatelessWidget {
  const DragBar({super.key});

  @override
  Widget build(BuildContext context) {
    return const DragToMoveArea(child: SizedBox.expand());
  }
}
