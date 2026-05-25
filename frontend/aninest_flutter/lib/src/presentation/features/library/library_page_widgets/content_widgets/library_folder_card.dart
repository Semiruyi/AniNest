import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

final Set<String> _loggedArtworkStates = <String>{};

class LibraryFolderCard extends StatelessWidget {
  const LibraryFolderCard({
    super.key,
    required this.folder,
    required this.imageUrl,
    required this.isSelected,
    required this.onPressed,
  });

  final LibraryFolderDto folder;
  final String? imageUrl;
  final bool isSelected;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
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
            image: _LibraryCardArtwork(title: folder.name, imageUrl: imageUrl),
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
    _logArtworkState();

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
                errorBuilder: (context, error, stackTrace) {
                  _logArtworkError(error);
                  return _ArtworkFallback(title: title);
                },
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

  void _logArtworkState() {
    final key = imageUrl == null
        ? 'missing:$title'
        : 'present:$title:$imageUrl';
    if (_loggedArtworkStates.contains(key)) {
      return;
    }

    _loggedArtworkStates.add(key);
    AppLogger.info(
      'LibraryFolderCard.Artwork',
      'Artwork state for "$title": imageUrl=${imageUrl ?? '<null>'}',
    );
  }

  void _logArtworkError(Object error) {
    final key = 'error:$title:$imageUrl';
    if (_loggedArtworkStates.contains(key)) {
      return;
    }

    _loggedArtworkStates.add(key);
    AppLogger.warning(
      'LibraryFolderCard.Artwork',
      'Failed to load artwork for "$title". imageUrl=$imageUrl, error=$error',
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
