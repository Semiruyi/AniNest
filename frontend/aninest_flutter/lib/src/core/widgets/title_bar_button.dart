import 'package:flutter/material.dart';

class TitleBarButton extends StatelessWidget {
  const TitleBarButton({
    super.key,
    required this.icon,
    required this.tooltip,
    required this.onPressed,
  });

  final IconData icon;
  final String tooltip;
  final Future<void> Function() onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 46,
      height: 44,
      child: IconButton(
        tooltip: tooltip,
        iconSize: 18,
        splashRadius: 20,
        onPressed: onPressed,
        icon: Icon(icon),
      ),
    );
  }
}
