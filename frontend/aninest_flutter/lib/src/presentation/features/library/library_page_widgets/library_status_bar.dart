import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryStatusBar extends StatelessWidget {
  const LibraryStatusBar({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      height: kLibraryStatusBarHeight,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: BoxDecoration(
        color: colorScheme.background,
        border: Border(top: BorderSide(color: colorScheme.border)),
      ),
      child: Row(
        children: <Widget>[
          const Text('128 items'),
          const Gap(16),
          Text(
            '0 selected',
            style: TextStyle(color: colorScheme.mutedForeground),
          ),
          const Spacer(),
          Text(
            'Library idle',
            style: TextStyle(color: colorScheme.mutedForeground),
          ),
        ],
      ),
    );
  }
}
