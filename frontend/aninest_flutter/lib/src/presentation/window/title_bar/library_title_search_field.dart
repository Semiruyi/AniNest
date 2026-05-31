import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryTitleSearchField extends StatefulWidget {
  const LibraryTitleSearchField({super.key, required this.controller});

  final AppController controller;

  @override
  State<LibraryTitleSearchField> createState() =>
      _LibraryTitleSearchFieldState();
}

class _LibraryTitleSearchFieldState extends State<LibraryTitleSearchField> {
  late final TextEditingController _textController;

  @override
  void initState() {
    super.initState();
    _textController = TextEditingController(
      text: widget.controller.librarySearchQuery,
    );
    widget.controller.addListener(_syncFromController);
  }

  @override
  void didUpdateWidget(covariant LibraryTitleSearchField oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.controller == widget.controller) {
      return;
    }

    oldWidget.controller.removeListener(_syncFromController);
    widget.controller.addListener(_syncFromController);
    _syncFromController();
  }

  @override
  void dispose() {
    widget.controller.removeListener(_syncFromController);
    _textController.dispose();
    super.dispose();
  }

  void _syncFromController() {
    final query = widget.controller.librarySearchQuery;
    if (_textController.text == query) {
      return;
    }

    _textController.value = _textController.value.copyWith(
      text: query,
      selection: TextSelection.collapsed(offset: query.length),
      composing: TextRange.empty,
    );
  }

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: _textController,
      style: const TextStyle(fontSize: 13, height: 0),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      placeholder: const Text('Search current view'),
      onChanged: widget.controller.setLibrarySearchQuery,
      features: <InputFeature>[
        InputFeature.leading(
          Icon(BootstrapIcons.search, size: 13),
          skipFocusTraversal: true,
        ),
      ],
    );
  }
}
