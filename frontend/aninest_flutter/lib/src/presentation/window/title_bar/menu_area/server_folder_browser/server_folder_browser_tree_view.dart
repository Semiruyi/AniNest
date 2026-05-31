import 'package:aninest_flutter/src/presentation/window/title_bar/menu_area/server_folder_browser/server_folder_browser_tree_state.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ServerFolderBrowserTreeView extends StatelessWidget {
  const ServerFolderBrowserTreeView({
    super.key,
    required this.state,
    required this.onSelectPath,
    required this.onToggleExpanded,
  });

  final ServerFolderBrowserTreeState state;
  final ValueChanged<String> onSelectPath;
  final Future<void> Function(String path, bool expanded) onToggleExpanded;

  @override
  Widget build(BuildContext context) {
    return TreeView<ServerFolderBrowserTreeItem>(
      nodes: state.nodes,
      allowMultiSelect: false,
      recursiveSelection: false,
      branchLine: BranchLine.path,
      padding: const EdgeInsets.all(8),
      onSelectionChanged: (selectedNodes, _, selected) {
        if (!selected || selectedNodes.isEmpty) {
          return;
        }

        final selectedNode = selectedNodes.last;
        if (selectedNode is! TreeItem<ServerFolderBrowserTreeItem>) {
          return;
        }

        onSelectPath(selectedNode.data.path);
      },
      builder: (context, node) {
        final item = node.data;
        return TreeItemView(
          leading: Icon(
            item.isRoot || node.expanded
                ? BootstrapIcons.folder2Open
                : BootstrapIcons.folder2,
            size: 16,
          ),
          trailing: item.isLoading
              ? const SizedBox.square(
                  dimension: 14,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : null,
          expandable: item.canExpand || item.isLoading,
          onPressed: () => onSelectPath(item.path),
          onDoublePressed: () => onSelectPath(item.path),
          onExpand: (expanded) {
            onToggleExpanded(item.path, expanded);
          },
          child: _ServerFolderBrowserTreeLabel(item: item),
        );
      },
    );
  }
}

class _ServerFolderBrowserTreeLabel extends StatelessWidget {
  const _ServerFolderBrowserTreeLabel({required this.item});

  final ServerFolderBrowserTreeItem item;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(item.name, maxLines: 1, overflow: TextOverflow.ellipsis),
        if (item.isRoot) ...<Widget>[
          const Gap(2),
          Text(
            item.path,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 11, color: colorScheme.mutedForeground),
          ),
        ],
      ],
    );
  }
}
