import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_folder_card_context_menu.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryFolderCard extends StatelessWidget {
  const LibraryFolderCard({
    super.key,
    required this.folder,
    required this.imageUrl,
    required this.isSelected,
    required this.onPressed,
    required this.onContextMenuRequested,
    required this.onOpen,
    required this.onToggleFavorite,
    required this.onSetWatchStatus,
    required this.onMoveToFront,
    required this.onDelete,
  });

  final LibraryFolderDto folder;
  final String? imageUrl;
  final bool isSelected;
  final VoidCallback onPressed;
  final VoidCallback onContextMenuRequested;
  final VoidCallback onOpen;
  final ValueChanged<bool> onToggleFavorite;
  final ValueChanged<WatchStatus> onSetWatchStatus;
  final VoidCallback onMoveToFront;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return LibraryFolderCardContextMenu(
      folder: folder,
      onContextMenuRequested: onContextMenuRequested,
      onOpen: onOpen,
      onToggleFavorite: onToggleFavorite,
      onSetWatchStatus: onSetWatchStatus,
      onMoveToFront: onMoveToFront,
      onDelete: onDelete,
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: isSelected ? colorScheme.primary : colorScheme.border,
            width: isSelected ? 1.5 : 1,
          ),
        ),
        child: SurfaceCard(
          child: SizedBox(
            height: 312,
            child: CardImage(
              direction: Axis.vertical,
              onPressed: onPressed,
              gap: 10,
              image: _LibraryCardArtwork(
                title: folder.name,
                imageUrl: imageUrl,
              ),
              title: _LibraryCardTitle(
                title: folder.name,
                watchStatus: folder.watchStatus,
                isFavorite: folder.isFavorite,
              ),
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
        ),
      ),
    );
  }

  String _subtitleFor(LibraryFolderDto folder) {
    if (folder.videoCount <= 0) {
      return 'No episodes detected';
    }
    return '${folder.playedCount} / ${folder.videoCount} episodes';
  }
}

class _LibraryCardTitle extends StatelessWidget {
  const _LibraryCardTitle({
    required this.title,
    required this.watchStatus,
    required this.isFavorite,
  });

  final String title;
  final WatchStatus watchStatus;
  final bool isFavorite;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ).semiBold(),
            ),
            if (isFavorite) ...<Widget>[
              const Gap(8),
              Icon(
                BootstrapIcons.heartFill,
                size: 14,
                color: colorScheme.destructive,
              ),
            ],
          ],
        ),
      ],
    );
  }
}

class _LibraryCardArtwork extends StatelessWidget {
  const _LibraryCardArtwork({required this.title, required this.imageUrl});

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
                width: double.infinity,
                height: double.infinity,
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
            style: TextStyle(fontSize: 12, color: colorScheme.mutedForeground),
          ),
        ),
      ],
    );
  }
}
