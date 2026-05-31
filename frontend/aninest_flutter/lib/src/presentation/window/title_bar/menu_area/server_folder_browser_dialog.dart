import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/menu_area/server_folder_browser/server_folder_browser_tree_state.dart';
import 'package:aninest_flutter/src/presentation/window/title_bar/menu_area/server_folder_browser/server_folder_browser_tree_view.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class ServerFolderBrowserDialog extends StatefulWidget {
  const ServerFolderBrowserDialog({
    super.key,
    required this.controller,
    this.initialPath,
  });

  final AppController controller;
  final String? initialPath;

  @override
  State<ServerFolderBrowserDialog> createState() =>
      _ServerFolderBrowserDialogState();
}

class _ServerFolderBrowserDialogState extends State<ServerFolderBrowserDialog> {
  ServerFolderBrowserTreeState? _treeState;
  String? _errorMessage;
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _loadInitialTree(widget.initialPath);
  }

  Future<void> _loadInitialTree(String? path) async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final initialBrowser = await widget.controller.browseLibraryDirectory(
        path,
      );
      if (!mounted) {
        return;
      }

      final rootBrowser = initialBrowser.currentPath == initialBrowser.rootPath
          ? initialBrowser
          : await widget.controller.browseLibraryDirectory(
              initialBrowser.rootPath,
            );
      if (!mounted) {
        return;
      }

      var treeState = ServerFolderBrowserTreeState.fromRootResponse(
        rootBrowser,
      );
      if (initialBrowser.currentPath != initialBrowser.rootPath) {
        final ancestorPaths = _buildAncestorPaths(
          initialBrowser.rootPath,
          initialBrowser.currentPath,
        );
        for (final ancestorPath in ancestorPaths) {
          final browser = ancestorPath == initialBrowser.currentPath
              ? initialBrowser
              : await widget.controller.browseLibraryDirectory(ancestorPath);
          if (!mounted) {
            return;
          }

          treeState = treeState
              .replaceNodeChildren(ancestorPath, browser.directories)
              .setNodeExpanded(ancestorPath, true);
        }

        treeState = treeState.selectPath(initialBrowser.currentPath);
      }

      setState(() {
        _treeState = treeState;
        _isLoading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _treeState = null;
        _isLoading = false;
        _errorMessage = '${error.code}: ${error.message}';
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _treeState = null;
        _isLoading = false;
        _errorMessage = error.toString();
      });
    }
  }

  void _handleSelectPath(String path) {
    final treeState = _treeState;
    if (treeState == null || treeState.selectedPath == path) {
      return;
    }

    setState(() {
      _treeState = treeState.selectPath(path);
      _errorMessage = null;
    });
  }

  Future<void> _handleToggleExpanded(String path, bool expanded) async {
    final treeState = _treeState;
    if (treeState == null) {
      return;
    }

    setState(() {
      _treeState = treeState.setNodeExpanded(path, expanded);
      _errorMessage = null;
    });

    final nextState = _treeState;
    if (!expanded || nextState == null || !nextState.shouldLoadChildren(path)) {
      return;
    }

    setState(() {
      _treeState = nextState.setNodeLoading(path, true);
    });

    try {
      final browser = await widget.controller.browseLibraryDirectory(path);
      if (!mounted) {
        return;
      }

      setState(() {
        _treeState = _treeState?.replaceNodeChildren(path, browser.directories);
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _treeState = _treeState?.setNodeLoading(path, false);
        _errorMessage = '${error.code}: ${error.message}';
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _treeState = _treeState?.setNodeLoading(path, false);
        _errorMessage = error.toString();
      });
    }
  }

  void _selectRoot() {
    final treeState = _treeState;
    if (treeState == null) {
      return;
    }

    setState(() {
      _treeState = treeState.selectPath(treeState.rootPath);
      _errorMessage = null;
    });
  }

  void _selectParent() {
    final treeState = _treeState;
    if (treeState == null) {
      return;
    }

    final parentPath = treeState.parentOf(treeState.selectedPath);
    if (parentPath == null || parentPath.isEmpty) {
      return;
    }

    setState(() {
      _treeState = treeState.selectPath(parentPath);
      _errorMessage = null;
    });
  }

  List<String> _buildAncestorPaths(String rootPath, String targetPath) {
    if (targetPath == rootPath || !targetPath.startsWith(rootPath)) {
      return const <String>[];
    }

    final separator = targetPath.contains('\\') ? '\\' : '/';
    final relativePath = targetPath
        .substring(rootPath.length)
        .replaceFirst(RegExp(r'^[\\/]'), '');
    if (relativePath.isEmpty) {
      return const <String>[];
    }

    final segments = relativePath
        .split(RegExp(r'[\\/]'))
        .where((segment) => segment.isNotEmpty);
    final ancestorPaths = <String>[];
    var currentPath = rootPath;
    for (final segment in segments) {
      currentPath = '$currentPath$separator$segment';
      ancestorPaths.add(currentPath);
    }

    return ancestorPaths;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    final treeState = _treeState;
    final selectedPath = treeState?.selectedPath;
    final canSelectCurrent =
        !_isLoading && selectedPath != null && selectedPath.isNotEmpty;
    final canNavigateUp =
        !_isLoading &&
        treeState != null &&
        treeState.parentOf(treeState.selectedPath) != null;

    return AlertDialog(
      title: Text(l10n.serverFolderBrowserDialogTitle),
      content: SizedBox(
        width: 560,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(l10n.serverFolderBrowserDialogMessage),
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: colorScheme.card,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: colorScheme.border),
              ),
              child: Text(
                selectedPath ?? l10n.serverFolderBrowserLoading,
                style: TextStyle(
                  fontSize: 12,
                  color: colorScheme.mutedForeground,
                ),
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: <Widget>[
                SecondaryButton(
                  onPressed: _isLoading || treeState == null
                      ? null
                      : _selectRoot,
                  child: Text(l10n.serverFolderBrowserRoot),
                ),
                const SizedBox(width: 8),
                SecondaryButton(
                  onPressed: canNavigateUp ? _selectParent : null,
                  child: Text(l10n.serverFolderBrowserUp),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Container(
              height: 320,
              width: double.infinity,
              decoration: BoxDecoration(
                color: colorScheme.background,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: colorScheme.border),
              ),
              child: _buildBody(context, colorScheme),
            ),
            if (_errorMessage != null) ...<Widget>[
              const SizedBox(height: 12),
              Text(
                _errorMessage!,
                style: TextStyle(fontSize: 12, color: colorScheme.destructive),
              ),
            ],
          ],
        ),
      ),
      actions: <Widget>[
        SecondaryButton(
          onPressed: _isLoading ? null : () => Navigator.of(context).pop(),
          child: Text(l10n.commonCancel),
        ),
        PrimaryButton(
          onPressed: !canSelectCurrent
              ? null
              : () => Navigator.of(context).pop(selectedPath),
          child: Text(l10n.serverFolderBrowserSelectCurrent),
        ),
      ],
    );
  }

  Widget _buildBody(BuildContext context, ColorScheme colorScheme) {
    final l10n = AppLocalizations.of(context);
    final treeState = _treeState;

    if (_isLoading && treeState == null) {
      return Center(
        child: Text(
          l10n.serverFolderBrowserLoading,
          style: TextStyle(color: colorScheme.mutedForeground),
        ),
      );
    }

    if (treeState == null) {
      return Center(
        child: Text(
          l10n.serverFolderBrowserUnavailable,
          style: TextStyle(color: colorScheme.mutedForeground),
        ),
      );
    }

    return ServerFolderBrowserTreeView(
      state: treeState,
      onSelectPath: _handleSelectPath,
      onToggleExpanded: _handleToggleExpanded,
    );
  }
}
