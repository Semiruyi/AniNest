import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
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
  LibraryBrowserResponse? _browser;
  String? _errorMessage;
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _load(widget.initialPath);
  }

  Future<void> _load(String? path) async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final browser = await widget.controller.browseLibraryDirectory(path);
      if (!mounted) {
        return;
      }

      setState(() {
        _browser = browser;
        _isLoading = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _browser = null;
        _isLoading = false;
        _errorMessage = '${error.code}: ${error.message}';
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _browser = null;
        _isLoading = false;
        _errorMessage = error.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    final browser = _browser;

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
                browser?.currentPath ?? l10n.serverFolderBrowserLoading,
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
                  onPressed: _isLoading
                      ? null
                      : () => _load(browser?.rootPath ?? widget.initialPath),
                  child: Text(l10n.serverFolderBrowserRoot),
                ),
                const SizedBox(width: 8),
                SecondaryButton(
                  onPressed: _isLoading || browser?.parentPath == null
                      ? null
                      : () => _load(browser!.parentPath),
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
          onPressed: _isLoading || browser == null || !browser.canSelect
              ? null
              : () => Navigator.of(context).pop(browser.currentPath),
          child: Text(l10n.serverFolderBrowserSelectCurrent),
        ),
      ],
    );
  }

  Widget _buildBody(BuildContext context, ColorScheme colorScheme) {
    final l10n = AppLocalizations.of(context);
    final browser = _browser;

    if (_isLoading && browser == null) {
      return Center(
        child: Text(
          l10n.serverFolderBrowserLoading,
          style: TextStyle(color: colorScheme.mutedForeground),
        ),
      );
    }

    if (browser == null) {
      return Center(
        child: Text(
          l10n.serverFolderBrowserUnavailable,
          style: TextStyle(color: colorScheme.mutedForeground),
        ),
      );
    }

    if (browser.directories.isEmpty) {
      return Center(
        child: Text(
          l10n.serverFolderBrowserEmpty,
          style: TextStyle(color: colorScheme.mutedForeground),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(8),
      itemCount: browser.directories.length,
      separatorBuilder: (_, _) => const SizedBox(height: 6),
      itemBuilder: (context, index) {
        final directory = browser.directories[index];
        return _DirectoryRow(
          directory: directory,
          onOpen: _isLoading ? null : () => _load(directory.path),
        );
      },
    );
  }
}

class _DirectoryRow extends StatelessWidget {
  const _DirectoryRow({required this.directory, required this.onOpen});

  final LibraryBrowserDirectoryDto directory;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return GestureDetector(
      onTap: onOpen,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          color: colorScheme.card,
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: colorScheme.border),
        ),
        child: Row(
          children: <Widget>[
            const Icon(BootstrapIcons.folder2Open, size: 16),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                directory.name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            const SizedBox(width: 8),
            Icon(
              BootstrapIcons.chevronRight,
              size: 14,
              color: colorScheme.mutedForeground,
            ),
          ],
        ),
      ),
    );
  }
}
