import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryStatusBar extends StatelessWidget {
  const LibraryStatusBar({super.key, required this.controller});

  final LibraryController controller;

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
      child: AnimatedBuilder(
        animation: controller,
        builder: (context, _) => Row(
          children: <Widget>[
            Text('${controller.folders.length} items'),
            const Gap(16),
            Text(
              controller.selectedFolderId == null ? '0 selected' : '1 selected',
              style: TextStyle(color: colorScheme.mutedForeground),
            ),
            const Spacer(),
            Text(
              controller.selectedFolder == null
                  ? 'Library idle'
                  : 'Selected ${controller.selectedFolder!.name}',
              style: TextStyle(color: colorScheme.mutedForeground),
            ),
          ],
        ),
      ),
    );
  }
}
