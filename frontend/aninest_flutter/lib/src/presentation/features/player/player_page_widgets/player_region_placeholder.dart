import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerRegionPlaceholder extends StatelessWidget {
  const PlayerRegionPlaceholder({
    super.key,
    required this.title,
    required this.color,
  });

  final String title;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.92),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: colorScheme.border.withValues(alpha: 0.5)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Align(
          alignment: Alignment.topLeft,
          child: Text(
            title,
            style: const TextStyle(color: Color(0xFFF8FAFC), fontSize: 13),
          ).semiBold(),
        ),
      ),
    );
  }
}
