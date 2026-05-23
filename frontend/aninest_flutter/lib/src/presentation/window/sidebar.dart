import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class Sidebar extends StatelessWidget {
  const Sidebar({super.key, this.width = 280});

  final double width;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SizedBox(
      width: width,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: colorScheme.card,
          border: Border(right: BorderSide(color: colorScheme.border)),
        ),
        child: Text("side bar"),
      ),
    );
  }
}
