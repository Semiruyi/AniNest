import 'dart:math' as math;

import 'package:animated_containers/animated_containers.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/library_shared.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryContentPane extends StatelessWidget {
  const LibraryContentPane({
    super.key,
    required this.controller,
  });

  final LibraryController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          AnimatedBuilder(
            animation: controller,
            builder: (context, _) => LibraryPaneHeader(
              title: 'All Media',
              subtitle: '${controller.folders.length} items • Animated wrap view',
            ),
          ),
          const Gap(12),
          Expanded(
            child: AnimatedBuilder(
              animation: controller,
              builder: (context, _) => _buildBody(context),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody(BuildContext context) {
    if (controller.folders.isEmpty) {
      return const _LibraryEmptyState();
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        final cardWidth = _resolveCardWidth(constraints.maxWidth);

        return SingleChildScrollView(
          padding: const EdgeInsets.only(bottom: 16),
          child: AnimatedWrap.material3(
            spacing: 12,
            runSpacing: 12,
            alignment: WrapAlignment.start,
            runAlignment: WrapAlignment.start,
            staggeredInitialInsertionAnimation: const Duration(
              milliseconds: 35,
            ),
            children: controller.folders
                .map(
                  (folder) => SizedBox(
                    key: ValueKey(folder.folderId),
                    width: cardWidth,
                    child: _LibraryCard(folder: folder),
                  ),
                )
                .toList(),
          ),
        );
      },
    );
  }

  double _resolveCardWidth(double maxWidth) {
    const spacing = 12.0;
    const minCardWidth = 210.0;
    const maxCardWidth = 280.0;

    final columns = math.max(
      1,
      ((maxWidth + spacing) / (minCardWidth + spacing)).floor(),
    );
    final width = (maxWidth - (columns - 1) * spacing) / columns;
    return width.clamp(minCardWidth, maxCardWidth);
  }
}

class _LibraryCard extends StatelessWidget {
  const _LibraryCard({required this.folder});

  final LibraryFolderDto folder;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return SurfaceCard(
      child: SizedBox(
        height: 312,
        child: CardImage(
          direction: Axis.vertical,
          onPressed: () {},
          gap: 10,
          image: _LibraryCardArtwork(
            title: _titleFor(folder),
            imageUrl: folder.coverUrl ?? folder.metadataSummary?.posterUrl,
          ),
          title: Text(_titleFor(folder)).semiBold(),
          subtitle: Text(
            _subtitleFor(folder),
            style: TextStyle(
              fontSize: 13,
              color: colorScheme.mutedForeground,
            ),
          ),
          backgroundColor: colorScheme.secondary,
          borderColor: colorScheme.border,
        ),
      ),
    );
  }

  String _titleFor(LibraryFolderDto folder) {
    final metadataTitle = folder.metadataSummary?.title;
    if (metadataTitle != null && metadataTitle.trim().isNotEmpty) {
      return metadataTitle;
    }
    return folder.name;
  }

  String _subtitleFor(LibraryFolderDto folder) {
    if (folder.videoCount <= 0) {
      return folder.path;
    }
    return '${folder.playedCount} / ${folder.videoCount} episodes';
  }
}

class _LibraryCardArtwork extends StatelessWidget {
  const _LibraryCardArtwork({
    required this.title,
    required this.imageUrl,
  });

  final String title;
  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return AspectRatio(
      aspectRatio: 0.72,
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(10),
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: <Color>[
              colorScheme.secondary,
              colorScheme.secondary.withValues(alpha: 0.72),
            ],
          ),
        ),
        clipBehavior: Clip.antiAlias,
        alignment: Alignment.center,
        child: imageUrl == null
            ? _ArtworkFallback(title: title)
            : Image.network(
                imageUrl!,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stackTrace) =>
                    _ArtworkFallback(title: title),
                loadingBuilder: (context, child, loadingProgress) {
                  if (loadingProgress == null) {
                    return child;
                  }
                  return _ArtworkFallback(title: title);
                },
              ),
      ),
    );
  }
}

class _ArtworkFallback extends StatelessWidget {
  const _ArtworkFallback({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Icon(
          BootstrapIcons.image,
          size: 28,
          color: colorScheme.mutedForeground,
        ),
        const Gap(8),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          child: Text(
            title,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 12,
              color: colorScheme.mutedForeground,
            ),
          ),
        ),
      ],
    );
  }
}

class _LibraryEmptyState extends StatelessWidget {
  const _LibraryEmptyState();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 320),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Icon(
              BootstrapIcons.collectionPlay,
              size: 32,
              color: colorScheme.mutedForeground,
            ),
            const Gap(12),
            const Text('No media folders yet').semiBold(),
            const Gap(6),
            Text(
              'Add a folder from the toolbar to start building your library.',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 13,
                color: colorScheme.mutedForeground,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
