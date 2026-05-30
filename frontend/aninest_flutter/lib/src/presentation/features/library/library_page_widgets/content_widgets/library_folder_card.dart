import 'dart:ui';

import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/presentation/features/library/library_page_widgets/content_widgets/library_folder_card_context_menu.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'primary_double_click_region.dart';

class LibraryFolderCard extends StatefulWidget {
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
  State<LibraryFolderCard> createState() => _LibraryFolderCardState();
}

class _LibraryFolderCardState extends State<LibraryFolderCard> {
  bool _isHovered = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    const cardRadius = 12.0;
    const borderRadius = 15.0;

    return LibraryFolderCardContextMenu(
      folder: widget.folder,
      onContextMenuRequested: widget.onContextMenuRequested,
      onOpen: widget.onOpen,
      onToggleFavorite: widget.onToggleFavorite,
      onSetWatchStatus: widget.onSetWatchStatus,
      onMoveToFront: widget.onMoveToFront,
      onDelete: widget.onDelete,
      child: PrimaryDoubleClickRegion(
        onDoubleClick: widget.onOpen,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 500),
          curve: Curves.easeInOut,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(borderRadius),
            border: Border.all(
              color: widget.isSelected
                  ? colorScheme.primary
                  : colorScheme.border,
              width: 2.3,
            ),
          ),
          child: MouseRegion(
            onEnter: (_) => setState(() => _isHovered = true),
            onExit: (_) => setState(() => _isHovered = false),
            child: SizedBox(
              height: 312,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(cardRadius),
                child: GestureDetector(
                  behavior: HitTestBehavior.opaque,
                  onTap: widget.onPressed,
                  child: Stack(
                    fit: StackFit.expand,
                    children: <Widget>[
                      _LibraryCardArtwork(
                        title: widget.folder.name,
                        imageUrl: widget.imageUrl,
                        isHovered: _isHovered,
                      ),
                      Align(
                        alignment: Alignment.bottomCenter,
                        child: FractionallySizedBox(
                          widthFactor: 1,
                          heightFactor: 1 / 4,
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(cardRadius),
                            child: BackdropFilter(
                              filter: ImageFilter.blur(sigmaX: 2, sigmaY: 2),
                              child: Container(
                                width: double.infinity,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                  vertical: 10,
                                ),
                                color: Colors.black.withValues(alpha: 0.3),
                                child: Center(
                                  child: _LibraryCardTitle(
                                    title: widget.folder.name,
                                  ),
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _LibraryCardTitle extends StatelessWidget {
  const _LibraryCardTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      maxLines: 2,
      textAlign: TextAlign.center,
      overflow: TextOverflow.ellipsis,
      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
    );
  }
}

class _LibraryCardArtwork extends StatelessWidget {
  const _LibraryCardArtwork({
    required this.title,
    required this.imageUrl,
    required this.isHovered,
  });

  final String title;
  final String? imageUrl;
  final bool isHovered;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return AnimatedScale(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOutCubic,
      scale: isHovered ? 1.06 : 1.0,
      child: Container(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: <Color>[
              colorScheme.secondary,
              colorScheme.secondary.withValues(alpha: 0.72),
            ],
          ),
        ),
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
