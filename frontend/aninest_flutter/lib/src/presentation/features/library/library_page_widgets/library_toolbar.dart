import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

const EdgeInsets _kToolbarPadding = EdgeInsets.symmetric(
  horizontal: 16,
  vertical: 10,
);

class LibraryToolbar extends StatelessWidget {
  const LibraryToolbar({super.key});

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: colorScheme.border)),
      ),
      child: SizedBox(
        height: kLibraryToolbarHeight,
        child: const Padding(padding: _kToolbarPadding, child: Row()),
      ),
    );
  }
}
