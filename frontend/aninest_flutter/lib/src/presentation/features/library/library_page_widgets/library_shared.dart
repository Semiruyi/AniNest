import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryPaneHeader extends StatelessWidget {
  const LibraryPaneHeader({
    super.key,
    required this.title,
    required this.subtitle,
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(title).semiBold(),
        const Gap(4),
        Text(
          subtitle,
          style: TextStyle(
            fontSize: 13,
            color: colorScheme.mutedForeground,
          ),
        ),
      ],
    );
  }
}
