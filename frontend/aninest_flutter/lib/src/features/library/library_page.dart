import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:flutter/material.dart';

class LibraryPage extends StatefulWidget {
  const LibraryPage({super.key, required this.controller});

  final AppController controller;

  @override
  State<LibraryPage> createState() => _LibraryPageState();
}

class _LibraryPageState extends State<LibraryPage> {
  final TextEditingController _pathController = TextEditingController();

  @override
  void dispose() {
    _pathController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _pathController,
                      decoration: const InputDecoration(
                        labelText: 'Library folder path',
                        hintText: r'D:\Anime\SomeFolder',
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  FilledButton(
                    onPressed: () async {
                      final path = _pathController.text.trim();
                      if (path.isEmpty) {
                        return;
                      }
                      await widget.controller.addFolder(path);
                      _pathController.clear();
                    },
                    child: const Text('Add Folder'),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Expanded(
                child: widget.controller.folders.isEmpty
                    ? const Center(
                        child: Text(
                          'No folders loaded yet. Start by importing from the backend debug page or API.',
                        ),
                      )
                    : ListView.separated(
                        itemCount: widget.controller.folders.length,
                        separatorBuilder: (_, index) =>
                            const SizedBox(height: 12),
                        itemBuilder: (context, index) {
                          final folder = widget.controller.folders[index];
                          return _FolderCard(
                            folder: folder,
                            isActive:
                                widget.controller.selectedFolderId ==
                                folder.folderId,
                            onOpen: () =>
                                widget.controller.openFolder(folder.folderId),
                            onFavorite: () =>
                                widget.controller.refreshLibrary(),
                          );
                        },
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _FolderCard extends StatelessWidget {
  const _FolderCard({
    required this.folder,
    required this.isActive,
    required this.onOpen,
    required this.onFavorite,
  });

  final LibraryFolderDto folder;
  final bool isActive;
  final VoidCallback onOpen;
  final VoidCallback onFavorite;

  @override
  Widget build(BuildContext context) {
    return Card(
      shape: RoundedRectangleBorder(
        side: BorderSide(
          color: isActive
              ? Theme.of(context).colorScheme.primary
              : Colors.white10,
        ),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    folder.name,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                if (folder.isFavorite)
                  const Icon(
                    Icons.favorite,
                    color: Colors.pinkAccent,
                    size: 18,
                  ),
              ],
            ),
            const SizedBox(height: 6),
            Text(folder.path, style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                Chip(label: Text('${folder.videoCount} videos')),
                Chip(label: Text(folder.watchStatus.name)),
                if (folder.metadataSummary?.title case final title?)
                  Chip(label: Text(title)),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                FilledButton(
                  onPressed: onOpen,
                  child: const Text('Open Session'),
                ),
                const SizedBox(width: 8),
                TextButton(onPressed: onFavorite, child: const Text('Sync')),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
