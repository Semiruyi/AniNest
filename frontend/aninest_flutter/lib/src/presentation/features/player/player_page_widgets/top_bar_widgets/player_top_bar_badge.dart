import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerTopBarBadge extends StatelessWidget {
  const PlayerTopBarBadge({super.key, required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: 22,
      constraints: const BoxConstraints(minWidth: 44),
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: 9),
      decoration: BoxDecoration(
        color: colorScheme.secondary,
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: colorScheme.border.withValues(alpha: 0.65)),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: colorScheme.foreground,
        ),
      ),
    );
  }
}
