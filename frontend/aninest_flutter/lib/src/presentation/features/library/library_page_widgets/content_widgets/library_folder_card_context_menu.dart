import 'package:flutter/gestures.dart';
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

    return ContextMenu(
      behavior: HitTestBehavior.translucent,
      items: _buildMenuItems(l10n),
      child: Listener(
        behavior: HitTestBehavior.translucent,
        onPointerDown: _handlePointerDown,
        child: child,
      ),
    );
  }

  void _handlePointerDown(PointerDownEvent event) {
    if (event.kind == PointerDeviceKind.mouse) {
      if ((event.buttons & kSecondaryMouseButton) != 0) {
        onContextMenuRequested();
      }
      return;
    }

    onContextMenuRequested();
  }

  List<MenuItem> _buildMenuItems(AppLocalizations l10n) {
    return <MenuItem>[
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
    ];
  }

  List<MenuItem> _buildWatchStatusMenuItems(AppLocalizations l10n) {
    return <MenuItem>[
      MenuRadioGroup<WatchStatus>(
        value: folder.watchStatus,
        onChanged: (_, value) => onSetWatchStatus(value),
        children: WatchStatus.values
            .map(
              (status) => MenuRadio<WatchStatus>(
                value: status,
                child: Text(_watchStatusLabel(l10n, status)),
              ),
            )
            .toList(),
      ),
    ];
  }

  String _watchStatusLabel(AppLocalizations l10n, WatchStatus status) {
    return switch (status) {
      WatchStatus.unknown => l10n.watchStatusUnknown,
      WatchStatus.watching => l10n.watchStatusWatching,
      WatchStatus.completed => l10n.watchStatusCompleted,
      WatchStatus.onHold => l10n.watchStatusOnHold,
      WatchStatus.dropped => l10n.watchStatusDropped,
      WatchStatus.planned => l10n.watchStatusPlanned,
    };
  }
}
