import 'package:aninest_flutter/src/presentation/window/app_page.dart';
import 'package:aninest_flutter/src/presentation/window/window_layout.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

enum _SidebarDestination {
  library(AppPage.library, 'Library', BootstrapIcons.collectionPlay),
  player(AppPage.player, 'Player', BootstrapIcons.playCircle),
  settings(AppPage.settings, 'Settings', BootstrapIcons.sliders);

  const _SidebarDestination(this.page, this.label, this.icon);

  final AppPage? page;
  final String label;
  final IconData icon;
}

class Sidebar extends StatelessWidget {
  const Sidebar({
    super.key,
    required this.currentPage,
    required this.onPageSelected,
  });

  final AppPage currentPage;
  final ValueChanged<AppPage> onPageSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    NavigationItem buildItem(_SidebarDestination destination) {
      return NavigationItem(
        selected: destination.page == currentPage,
        onChanged: (selected) {
          if (!selected) {
            return;
          }
          onPageSelected(destination.page!);
        },
        label: Text(destination.label),
        child: Icon(destination.icon),
      );
    }

    return DecoratedBox(
      decoration: BoxDecoration(
        color: colorScheme.card,
        border: Border(right: BorderSide(color: colorScheme.border)),
      ),
      child: NavigationRail(
        alignment: _alignment,
        labelType: _labelType,
        labelPosition: _labelPosition,
        expanded: _expanded,
        collapsedSize: kSidebarRailWidth,
        padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 8),
        children: <Widget>[
          buildItem(_SidebarDestination.library),
          buildItem(_SidebarDestination.player),
          const NavigationDivider(),
          NavigationGroup(
            label: const Text('System'),
            children: <Widget>[buildItem(_SidebarDestination.settings)],
          ),
        ],
      ),
    );
  }

  static const NavigationRailAlignment _alignment =
      NavigationRailAlignment.start;
  static const NavigationLabelType _labelType = NavigationLabelType.none;
  static const NavigationLabelPosition _labelPosition =
      NavigationLabelPosition.bottom;
  static const bool _expanded = false;
}
