import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_layout.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryInspectorPane extends StatelessWidget {
  const LibraryInspectorPane({super.key, required this.controller});

  final LibraryController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.card,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(kLibraryPanePadding),
        child: AnimatedBuilder(
          animation: controller,
          builder: (context, _) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              const LibraryPaneHeader(
                title: 'Inspector',
                subtitle: 'Selection and quick actions',
              ),
              
            ],
          ),
        ),
      ),
    );
  }
}
