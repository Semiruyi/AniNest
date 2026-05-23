import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryPage extends StatelessWidget {
  const LibraryPage({super.key, required this.controller});

  final LibraryController controller;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(color: Color(0xFF1D4ED8)),
      child: const SizedBox.expand(),
    );
  }
}
