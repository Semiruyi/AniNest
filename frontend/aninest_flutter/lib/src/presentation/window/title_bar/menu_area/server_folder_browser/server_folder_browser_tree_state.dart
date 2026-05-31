import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ServerFolderBrowserTreeItem {
  const ServerFolderBrowserTreeItem({
    required this.name,
    required this.path,
    required this.canExpand,
    required this.childrenLoaded,
    required this.isLoading,
    required this.isRoot,
  });

  final String name;
  final String path;
  final bool canExpand;
  final bool childrenLoaded;
  final bool isLoading;
  final bool isRoot;

  factory ServerFolderBrowserTreeItem.root({
    required String path,
    required bool canExpand,
  }) {
    return ServerFolderBrowserTreeItem(
      name: displayNameForPath(path),
      path: path,
      canExpand: canExpand,
      childrenLoaded: true,
      isLoading: false,
      isRoot: true,
    );
  }

  factory ServerFolderBrowserTreeItem.directory(
    LibraryBrowserDirectoryDto directory,
  ) {
    return ServerFolderBrowserTreeItem(
      name: directory.name,
      path: directory.path,
      canExpand: true,
      childrenLoaded: false,
      isLoading: false,
      isRoot: false,
    );
  }

  ServerFolderBrowserTreeItem copyWith({
    String? name,
    String? path,
    bool? canExpand,
    bool? childrenLoaded,
    bool? isLoading,
    bool? isRoot,
  }) {
    return ServerFolderBrowserTreeItem(
      name: name ?? this.name,
      path: path ?? this.path,
      canExpand: canExpand ?? this.canExpand,
      childrenLoaded: childrenLoaded ?? this.childrenLoaded,
      isLoading: isLoading ?? this.isLoading,
      isRoot: isRoot ?? this.isRoot,
    );
  }

  static String displayNameForPath(String path) {
    final normalizedPath = path.replaceAll('\\', '/');
    final segments = normalizedPath.split('/').where((item) => item.isNotEmpty);
    if (segments.isEmpty) {
      return path;
    }

    return segments.last;
  }

  @override
  bool operator ==(Object other) {
    if (identical(this, other)) {
      return true;
    }

    return other is ServerFolderBrowserTreeItem &&
        other.name == name &&
        other.path == path &&
        other.canExpand == canExpand &&
        other.childrenLoaded == childrenLoaded &&
        other.isLoading == isLoading &&
        other.isRoot == isRoot;
  }

  @override
  int get hashCode =>
      Object.hash(name, path, canExpand, childrenLoaded, isLoading, isRoot);
}

class ServerFolderBrowserTreeState {
  const ServerFolderBrowserTreeState({
    required this.rootPath,
    required this.selectedPath,
    required this.nodes,
  });

  final String rootPath;
  final String selectedPath;
  final List<TreeNode<ServerFolderBrowserTreeItem>> nodes;

  factory ServerFolderBrowserTreeState.fromRootResponse(
    LibraryBrowserResponse response,
  ) {
    final children = _buildChildNodes(response.directories);
    return ServerFolderBrowserTreeState(
      rootPath: response.rootPath,
      selectedPath: response.currentPath,
      nodes: <TreeNode<ServerFolderBrowserTreeItem>>[
        TreeItem<ServerFolderBrowserTreeItem>(
          data: ServerFolderBrowserTreeItem.root(
            path: response.rootPath,
            canExpand: children.isNotEmpty,
          ),
          expanded: true,
          selected: true,
          children: children,
        ),
      ],
    );
  }

  ServerFolderBrowserTreeState copyWith({
    String? rootPath,
    String? selectedPath,
    List<TreeNode<ServerFolderBrowserTreeItem>>? nodes,
  }) {
    return ServerFolderBrowserTreeState(
      rootPath: rootPath ?? this.rootPath,
      selectedPath: selectedPath ?? this.selectedPath,
      nodes: nodes ?? this.nodes,
    );
  }

  String? parentOf(String path) => _findParentPath(nodes, path);

  bool shouldLoadChildren(String path) {
    final item = _findItem(nodes, path);
    return item != null && !item.childrenLoaded;
  }

  ServerFolderBrowserTreeState selectPath(String path) {
    return copyWith(
      selectedPath: path,
      nodes: _mapNodes(nodes, (node) {
        final isSelected = node.data.path == path;
        if (node.selected == isSelected) {
          return node;
        }

        return _copyNode(node, selected: isSelected);
      }),
    );
  }

  ServerFolderBrowserTreeState setNodeExpanded(String path, bool expanded) {
    return copyWith(
      nodes: _mapNodes(nodes, (node) {
        if (node.data.path != path || node.expanded == expanded) {
          return node;
        }

        return _copyNode(node, expanded: expanded);
      }),
    );
  }

  ServerFolderBrowserTreeState setNodeLoading(String path, bool isLoading) {
    return copyWith(
      nodes: _mapNodes(nodes, (node) {
        if (node.data.path != path || node.data.isLoading == isLoading) {
          return node;
        }

        return _copyNode(node, data: node.data.copyWith(isLoading: isLoading));
      }),
    );
  }

  ServerFolderBrowserTreeState replaceNodeChildren(
    String path,
    List<LibraryBrowserDirectoryDto> directories,
  ) {
    final children = _buildChildNodes(directories);

    return copyWith(
      nodes: _mapNodes(nodes, (node) {
        if (node.data.path != path) {
          return node;
        }

        return _copyNode(
          node,
          data: node.data.copyWith(
            canExpand: children.isNotEmpty,
            childrenLoaded: true,
            isLoading: false,
          ),
          children: children,
          expanded: children.isEmpty ? false : node.expanded,
        );
      }),
    );
  }

  static List<TreeNode<ServerFolderBrowserTreeItem>> _buildChildNodes(
    List<LibraryBrowserDirectoryDto> directories,
  ) {
    return directories
        .map(
          (directory) => TreeItem<ServerFolderBrowserTreeItem>(
            data: ServerFolderBrowserTreeItem.directory(directory),
          ),
        )
        .toList(growable: false);
  }

  static TreeItem<ServerFolderBrowserTreeItem> _copyNode(
    TreeItem<ServerFolderBrowserTreeItem> node, {
    ServerFolderBrowserTreeItem? data,
    List<TreeNode<ServerFolderBrowserTreeItem>>? children,
    bool? expanded,
    bool? selected,
  }) {
    return TreeItem<ServerFolderBrowserTreeItem>(
      data: data ?? node.data,
      children: children ?? node.children,
      expanded: expanded ?? node.expanded,
      selected: selected ?? node.selected,
    );
  }

  static List<TreeNode<ServerFolderBrowserTreeItem>> _mapNodes(
    List<TreeNode<ServerFolderBrowserTreeItem>> nodes,
    TreeItem<ServerFolderBrowserTreeItem> Function(
      TreeItem<ServerFolderBrowserTreeItem> node,
    )
    transform,
  ) {
    return nodes
        .map((node) {
          if (node is! TreeItem<ServerFolderBrowserTreeItem>) {
            return node;
          }

          final mappedChildren = _mapNodes(node.children, transform);
          final mappedNode = _copyNode(node, children: mappedChildren);
          return transform(mappedNode);
        })
        .toList(growable: false);
  }

  static ServerFolderBrowserTreeItem? _findItem(
    List<TreeNode<ServerFolderBrowserTreeItem>> nodes,
    String path,
  ) {
    for (final node in nodes) {
      if (node is! TreeItem<ServerFolderBrowserTreeItem>) {
        continue;
      }

      if (node.data.path == path) {
        return node.data;
      }

      final descendant = _findItem(node.children, path);
      if (descendant != null) {
        return descendant;
      }
    }

    return null;
  }

  static String? _findParentPath(
    List<TreeNode<ServerFolderBrowserTreeItem>> nodes,
    String path, {
    String? parentPath,
  }) {
    for (final node in nodes) {
      if (node is! TreeItem<ServerFolderBrowserTreeItem>) {
        continue;
      }

      if (node.data.path == path) {
        return parentPath;
      }

      final descendant = _findParentPath(
        node.children,
        path,
        parentPath: node.data.path,
      );
      if (descendant != null) {
        return descendant;
      }
    }

    return null;
  }
}
