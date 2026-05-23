import 'package:shadcn_flutter/shadcn_flutter.dart';

class MenuBar extends StatelessWidget {
  const MenuBar({super.key, required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: Align(alignment: Alignment.centerLeft, child: Text(title)),
    );
  }
}
