import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class LibraryFolderCardContextMenu extends StatelessWidget {
  const LibraryFolderCardContextMenu({
    super.key,
    required this.folder,
    required this.child,
    required this.onContextMenuRequested,
    required this.onOpen,
    required this.onToggleFavorite,
    required this.onSetWatchStatus,
    required this.onMoveToFront,
    required this.onDelete,
  });

  final LibraryFolderDto folder;
  final Widget child;
  final VoidCallback onContextMenuRequested;
  final VoidCallback onOpen;
  final ValueChanged<bool> onToggleFavorite;
  final ValueChanged<WatchStatus> onSetWatchStatus;
  final VoidCallback onMoveToFront;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final theme = Theme.of(context);
    final enableLongPress =
        theme.platform == TargetPlatform.iOS ||
        theme.platform == TargetPlatform.android ||
        theme.platform == TargetPlatform.fuchsia;

    return GestureDetector(
      behavior: HitTestBehavior.translucent,
      onSecondaryTapDown: (details) {
        onContextMenuRequested();
        _showContextMenu(context, details.globalPosition, <MenuItem>[
          MenuButton(
            leading: const Icon(BootstrapIcons.playFill),
            onPressed: (_) => onOpen(),
            child: Text(l10n.libraryCardMenuOpen),
          ),
          const MenuDivider(),
          MenuCheckbox(
            value: folder.isFavorite,
            onChanged: (_, value) => onToggleFavorite(value),
            child: Text(l10n.libraryCardMenuFavorite),
          ),
          MenuButton(
            leading: const Icon(BootstrapIcons.eye),
            subMenu: _buildWatchStatusMenuItems(l10n),
            child: Text(l10n.libraryCardMenuWatchStatus),
          ),
          const MenuDivider(),
          MenuButton(
            leading: const Icon(BootstrapIcons.arrowUpSquare),
            onPressed: (_) => onMoveToFront(),
            child: Text(l10n.libraryCardMenuMoveToFront),
          ),
          const MenuDivider(),
          MenuButton(
            leading: const Icon(BootstrapIcons.trash3),
            onPressed: (_) => onDelete(),
            child: Text(l10n.libraryCardMenuDelete),
          ),
        ]);
      },
      onLongPressStart: enableLongPress
          ? (details) {
              onContextMenuRequested();
              _showContextMenu(context, details.globalPosition, <MenuItem>[
                MenuButton(
                  leading: const Icon(BootstrapIcons.playFill),
                  onPressed: (_) => onOpen(),
                  child: Text(l10n.libraryCardMenuOpen),
                ),
                const MenuDivider(),
                MenuCheckbox(
                  value: folder.isFavorite,
                  onChanged: (_, value) => onToggleFavorite(value),
                  child: Text(l10n.libraryCardMenuFavorite),
                ),
                MenuButton(
                  leading: const Icon(BootstrapIcons.eye),
                  subMenu: _buildWatchStatusMenuItems(l10n),
                  child: Text(l10n.libraryCardMenuWatchStatus),
                ),
                const MenuDivider(),
                MenuButton(
                  leading: const Icon(BootstrapIcons.arrowUpSquare),
                  onPressed: (_) => onMoveToFront(),
                  child: Text(l10n.libraryCardMenuMoveToFront),
                ),
                const MenuDivider(),
                MenuButton(
                  leading: const Icon(BootstrapIcons.trash3),
                  onPressed: (_) => onDelete(),
                  child: Text(l10n.libraryCardMenuDelete),
                ),
              ]);
            }
          : null,
      child: child,
    );
  }

  Future<void> _showContextMenu(
    BuildContext context,
    Offset position,
    List<MenuItem> items,
  ) {
    final key = GlobalKey<OverlayHandlerStateMixin>();
    final theme = Theme.of(context);
    final overlayManager = OverlayManager.of(context);

    return overlayManager
        .showMenu<void>(
          key: key,
          context: context,
          position: position + const Offset(8, 0),
          alignment: Alignment.topLeft,
          anchorAlignment: Alignment.topRight,
          regionGroupId: key,
          modal: true,
          follow: false,
          consumeOutsideTaps: false,
          dismissBackdropFocus: false,
          overlayBarrier: OverlayBarrier(
            borderRadius: BorderRadius.circular(theme.radiusMd),
            barrierColor: const Color(0xB2000000),
          ),
          builder: (context) {
            return ConstrainedBox(
              constraints: const BoxConstraints(minWidth: 192),
              child: MenuGroup(
                itemPadding: EdgeInsets.zero,
                direction: Axis.vertical,
                regionGroupId: key,
                subMenuOffset: const Offset(8, -4),
                onDismissed: () {
                  closeOverlay(context);
                },
                builder: (context, children) {
                  return MenuPopup(children: children);
                },
                children: items,
              ),
            );
          },
        )
        .future;
  }

  List<MenuItem> _buildWatchStatusMenuItems(AppLocalizations l10n) {
    return <MenuItem>[
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.unknown,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.unknown),
        child: Text(l10n.watchStatusUnknown),
      ),
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.watching,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.watching),
        child: Text(l10n.watchStatusWatching),
      ),
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.completed,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.completed),
        child: Text(l10n.watchStatusCompleted),
      ),
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.onHold,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.onHold),
        child: Text(l10n.watchStatusOnHold),
      ),
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.dropped,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.dropped),
        child: Text(l10n.watchStatusDropped),
      ),
      MenuCheckbox(
        value: folder.watchStatus == WatchStatus.planned,
        onChanged: (_, _) => onSetWatchStatus(WatchStatus.planned),
        child: Text(l10n.watchStatusPlanned),
      ),
    ];
  }
}
