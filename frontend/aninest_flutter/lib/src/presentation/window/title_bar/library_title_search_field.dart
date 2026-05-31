import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryTitleSearchField extends StatelessWidget {
  const LibraryTitleSearchField({super.key});

  @override
  Widget build(BuildContext context) {
    return TextField(
      style: const TextStyle(fontSize: 13, height: 0),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      placeholder: const Text('Search current view'),
      features: <InputFeature>[
        InputFeature.leading(
          Icon(BootstrapIcons.search, size: 13),
          skipFocusTraversal: true,
        ),
      ],
    );
  }
}
