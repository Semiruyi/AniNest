import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerMenuPopupShell extends StatelessWidget {
  const PlayerMenuPopupShell({
    super.key,
    required this.minWidth,
    this.maxWidth,
    required this.onDismissRequested,
    required this.children,
  });

  final double minWidth;
  final double? maxWidth;
  final VoidCallback onDismissRequested;
  final List<MenuItem> children;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(
        minWidth: minWidth,
        maxWidth: maxWidth ?? double.infinity,
      ),
      child: MenuGroup(
        itemPadding: EdgeInsets.zero,
        direction: Axis.vertical,
        onDismissed: onDismissRequested,
        builder: (BuildContext context, List<Widget> children) {
          return MenuPopup(children: children);
        },
        children: children,
      ),
    );
  }
}
