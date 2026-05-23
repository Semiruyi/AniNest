import 'package:shadcn_flutter/shadcn_flutter.dart';
import 'package:aninest_flutter/src/presentation/window/window_layout.dart';

enum _SidebarDestination {
  library('Library', BootstrapIcons.collectionPlay),
  player('Player', BootstrapIcons.playCircle),
  settings('Settings', BootstrapIcons.sliders);

  const _SidebarDestination(this.label, this.icon);

  final String label;
  final IconData icon;
}

class Sidebar extends StatefulWidget {
  const Sidebar({super.key});

  @override
  State<Sidebar> createState() => _SidebarState();
}

class _SidebarState extends State<Sidebar> {
  static const NavigationRailAlignment _alignment =
      NavigationRailAlignment.start;
  static const NavigationLabelType _labelType = NavigationLabelType.none;
  static const NavigationLabelPosition _labelPosition =
      NavigationLabelPosition.bottom;
  static const bool _expanded = false;
  _SidebarDestination _selected = _SidebarDestination.library;

  NavigationItem _buildItem(_SidebarDestination destination) {
    return NavigationItem(
      selected: _selected == destination,
      onChanged: (selected) {
        if (!selected) {
          return;
        }
        setState(() {
          _selected = destination;
        });
      },
      label: Text(destination.label),
      child: Icon(destination.icon),
    );
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

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
          _buildItem(_SidebarDestination.library),
          _buildItem(_SidebarDestination.player),
          const NavigationDivider(),
          NavigationGroup(
            label: const Text('System'),
            children: <Widget>[_buildItem(_SidebarDestination.settings)],
          ),
        ],
      ),
    );
  }
}
