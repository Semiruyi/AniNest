import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class BackendConnectionDialog extends StatefulWidget {
  const BackendConnectionDialog({super.key, required this.controller});

  final AppController controller;

  @override
  State<BackendConnectionDialog> createState() =>
      _BackendConnectionDialogState();
}

class _BackendConnectionDialogState extends State<BackendConnectionDialog> {
  late final TextEditingController _baseUrlController;

  String? _statusMessage;
  bool _statusIsError = false;
  bool _isTesting = false;
  bool _isSaving = false;

  bool get _isBusy => _isTesting || _isSaving;

  @override
  void initState() {
    super.initState();
    _baseUrlController = TextEditingController(text: widget.controller.baseUrl);
  }

  @override
  void dispose() {
    _baseUrlController.dispose();
    super.dispose();
  }

  Future<void> _handleTest() async {
    FocusScope.of(context).unfocus();
    setState(() {
      _isTesting = true;
      _statusMessage = null;
      _statusIsError = false;
    });

    final l10n = AppLocalizations.of(context);
    final error = await widget.controller.testBaseUrl(_baseUrlController.text);
    if (!mounted) {
      return;
    }

    setState(() {
      _isTesting = false;
      _statusMessage = error ?? l10n.backendConnectionTestSuccess;
      _statusIsError = error != null;
    });
  }

  Future<void> _handleSave() async {
    FocusScope.of(context).unfocus();
    setState(() {
      _isSaving = true;
      _statusMessage = null;
      _statusIsError = false;
    });

    final testError = await widget.controller.testBaseUrl(
      _baseUrlController.text,
    );
    if (!mounted) {
      return;
    }

    if (testError != null) {
      setState(() {
        _isSaving = false;
        _statusMessage = testError;
        _statusIsError = true;
      });
      return;
    }

    final saveError = await widget.controller.updateBaseUrl(
      _baseUrlController.text,
    );
    if (!mounted) {
      return;
    }

    if (saveError != null) {
      setState(() {
        _isSaving = false;
        _statusMessage = saveError;
        _statusIsError = true;
      });
      return;
    }

    Navigator.of(context).pop(widget.controller.baseUrl);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final colorScheme = Theme.of(context).colorScheme;

    return AlertDialog(
      title: Text(l10n.backendConnectionDialogTitle),
      content: SizedBox(
        width: 440,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(l10n.backendConnectionDialogMessage),
            const SizedBox(height: 12),
            TextField(
              controller: _baseUrlController,
              keyboardType: TextInputType.url,
              autofocus: true,
              placeholder: Text(l10n.backendConnectionUrlPlaceholder),
              onChanged: (_) {
                if (_statusMessage == null) {
                  return;
                }
                setState(() {
                  _statusMessage = null;
                  _statusIsError = false;
                });
              },
            ),
            const SizedBox(height: 8),
            Text(
              l10n.backendConnectionDialogHint,
              style: TextStyle(
                fontSize: 12,
                color: colorScheme.mutedForeground,
              ),
            ),
            if (_statusMessage != null) ...<Widget>[
              const SizedBox(height: 12),
              Text(
                _statusMessage!,
                style: TextStyle(
                  fontSize: 12,
                  color: _statusIsError
                      ? colorScheme.destructive
                      : colorScheme.mutedForeground,
                ),
              ),
            ],
          ],
        ),
      ),
      actions: <Widget>[
        SecondaryButton(
          onPressed: _isBusy
              ? null
              : () {
                  _baseUrlController.text = AppController.defaultBaseUrl;
                  setState(() {
                    _statusMessage = null;
                    _statusIsError = false;
                  });
                },
          child: Text(l10n.backendConnectionUseDefault),
        ),
        SecondaryButton(
          onPressed: _isBusy ? null : _handleTest,
          child: Text(
            _isTesting
                ? l10n.backendConnectionTesting
                : l10n.backendConnectionTest,
          ),
        ),
        SecondaryButton(
          onPressed: _isBusy ? null : () => Navigator.of(context).pop(),
          child: Text(l10n.commonCancel),
        ),
        PrimaryButton(
          onPressed: _isBusy ? null : _handleSave,
          child: Text(
            _isSaving
                ? l10n.backendConnectionSaving
                : l10n.backendConnectionSave,
          ),
        ),
      ],
    );
  }
}
