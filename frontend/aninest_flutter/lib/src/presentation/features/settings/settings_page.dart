import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class SettingsPage extends StatefulWidget {
  const SettingsPage({
    super.key,
    required this.controller,
    required this.isPresented,
  });

  final AppController controller;
  final bool isPresented;

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  late final TextEditingController _bangumiTokenController;
  late final TextEditingController _proxyUrlController;

  bool _autoScrapeMetadata = false;
  bool _isSaving = false;
  bool _isDirty = false;
  String? _statusMessage;
  bool _statusIsError = false;

  @override
  void initState() {
    super.initState();
    _bangumiTokenController = TextEditingController();
    _proxyUrlController = TextEditingController();
    widget.controller.addListener(_handleControllerChanged);
    _syncFromController(force: true);
  }

  @override
  void didUpdateWidget(covariant SettingsPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.controller != widget.controller) {
      oldWidget.controller.removeListener(_handleControllerChanged);
      widget.controller.addListener(_handleControllerChanged);
      _syncFromController(force: true);
    }
  }

  @override
  void dispose() {
    widget.controller.removeListener(_handleControllerChanged);
    _bangumiTokenController.dispose();
    _proxyUrlController.dispose();
    super.dispose();
  }

  void _handleControllerChanged() {
    if (!mounted || _isDirty) {
      return;
    }

    _syncFromController();
  }

  void _syncFromController({bool force = false}) {
    final settings = widget.controller.appSettings;
    if (settings == null) {
      return;
    }

    final metadata = settings.metadata;
    final nextToken = metadata.bangumiAccessToken ?? '';
    final nextProxy = metadata.metadataProxyUrl ?? '';
    final shouldUpdate = force ||
        _autoScrapeMetadata != metadata.autoScrapeMetadata ||
        _bangumiTokenController.text != nextToken ||
        _proxyUrlController.text != nextProxy;
    if (!shouldUpdate) {
      return;
    }

    setState(() {
      _autoScrapeMetadata = metadata.autoScrapeMetadata;
      _bangumiTokenController.text = nextToken;
      _proxyUrlController.text = nextProxy;
      _isDirty = false;
      _statusMessage = null;
      _statusIsError = false;
    });
  }

  void _markDirty() {
    if (_isDirty) {
      return;
    }

    setState(() {
      _isDirty = true;
      _statusMessage = null;
      _statusIsError = false;
    });
  }

  Future<void> _handleSave() async {
    FocusScope.of(context).unfocus();
    setState(() {
      _isSaving = true;
      _statusMessage = null;
      _statusIsError = false;
    });

    final settings = MetadataSettingsDto(
      autoScrapeMetadata: _autoScrapeMetadata,
      bangumiAccessToken: _normalizeText(_bangumiTokenController.text),
      metadataProxyUrl: _normalizeText(_proxyUrlController.text),
    );

    try {
      await widget.controller.saveMetadataSettings(settings);
      if (!mounted) {
        return;
      }

      setState(() {
        _isSaving = false;
        _isDirty = false;
        _statusMessage = 'Saved';
        _statusIsError = false;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }

      setState(() {
        _isSaving = false;
        _statusMessage = error.toString();
        _statusIsError = true;
      });
    }
  }

  void _handleReset() {
    _syncFromController(force: true);
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final settings = widget.controller.appSettings;

    return Container(
      color: colorScheme.background,
      child: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 1200),
              child: settings == null
                  ? const SizedBox(
                      height: 280,
                      child: Center(child: Text('Loading settings...')),
                    )
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        _buildHeader(context, colorScheme),
                        const SizedBox(height: 16),
                        LayoutBuilder(
                          builder: (context, constraints) {
                            final wide = constraints.maxWidth >= 980;
                            final form = _buildMetadataForm(context, colorScheme);
                            final summary = _buildSummaryPanel(
                              context,
                              colorScheme,
                              settings.metadata,
                            );

                            if (!wide) {
                              return Column(
                                children: <Widget>[
                                  form,
                                  const SizedBox(height: 16),
                                  summary,
                                ],
                              );
                            }

                            return Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: <Widget>[
                                Expanded(flex: 2, child: form),
                                const SizedBox(width: 16),
                                Expanded(flex: 1, child: summary),
                              ],
                            );
                          },
                        ),
                        const SizedBox(height: 16),
                        _buildActionBar(context, colorScheme),
                      ],
                    ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context, ColorScheme colorScheme) {
    final titleStyle = TextStyle(
      fontSize: 24,
      fontWeight: FontWeight.w600,
      color: colorScheme.foreground,
    );
    final subtitleStyle = TextStyle(
      fontSize: 13,
      color: colorScheme.mutedForeground,
    );

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.card,
        border: Border.all(color: colorScheme.border),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text('Settings', style: titleStyle),
                const SizedBox(height: 6),
                Text('Metadata', style: subtitleStyle),
              ],
            ),
          ),
          _StatusChip(
            label: _isDirty ? 'Unsaved' : 'Synced',
            accent: _isDirty
                ? colorScheme.primary
                : colorScheme.mutedForeground,
          ),
        ],
      ),
    );
  }

  Widget _buildMetadataForm(BuildContext context, ColorScheme colorScheme) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.card,
        border: Border.all(color: colorScheme.border),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const _SectionTitle('Metadata source'),
          const SizedBox(height: 16),
          _ToggleRow(
            title: 'Auto scrape metadata',
            subtitle: 'Queue metadata fetches automatically',
            value: _autoScrapeMetadata,
            onChanged: _isSaving
                ? null
                : (value) {
                    setState(() {
                      _autoScrapeMetadata = value;
                    });
                    _markDirty();
                  },
          ),
          const SizedBox(height: 20),
          LayoutBuilder(
            builder: (context, constraints) {
              final split = constraints.maxWidth >= 700;
              final tokenField = _LabeledTextField(
                label: 'Bangumi access token',
                controller: _bangumiTokenController,
                placeholder: 'optional',
                enabled: !_isSaving,
                onChanged: (_) => _markDirty(),
              );
              final proxyField = _LabeledTextField(
                label: 'Metadata proxy URL',
                controller: _proxyUrlController,
                placeholder: 'http://127.0.0.1:7890',
                enabled: !_isSaving,
                keyboardType: TextInputType.url,
                onChanged: (_) => _markDirty(),
              );

              if (!split) {
                return Column(
                  children: <Widget>[
                    tokenField,
                    const SizedBox(height: 16),
                    proxyField,
                  ],
                );
              }

              return Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Expanded(child: tokenField),
                  const SizedBox(width: 16),
                  Expanded(child: proxyField),
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _buildSummaryPanel(
    BuildContext context,
    ColorScheme colorScheme,
    MetadataSettingsDto metadata,
  ) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: colorScheme.card,
        border: Border.all(color: colorScheme.border),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const _SectionTitle('Current state'),
          const SizedBox(height: 16),
          _KeyValueRow(
            label: 'Auto scrape',
            value: metadata.autoScrapeMetadata ? 'On' : 'Off',
          ),
          const SizedBox(height: 12),
          _KeyValueRow(
            label: 'Bangumi token',
            value: metadata.bangumiAccessToken == null ||
                    metadata.bangumiAccessToken!.isEmpty
                ? 'Not set'
                : 'Configured',
          ),
          const SizedBox(height: 12),
          _KeyValueRow(
            label: 'Proxy',
            value: metadata.metadataProxyUrl == null ||
                    metadata.metadataProxyUrl!.isEmpty
                ? 'Direct'
                : metadata.metadataProxyUrl!,
          ),
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: colorScheme.background,
              border: Border.all(color: colorScheme.border),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              'Metadata requests follow the saved proxy when present.',
              style: TextStyle(
                fontSize: 12,
                color: colorScheme.mutedForeground,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildActionBar(BuildContext context, ColorScheme colorScheme) {
    final statusStyle = TextStyle(
      fontSize: 12,
      color: _statusIsError
          ? colorScheme.destructive
          : colorScheme.mutedForeground,
    );

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Wrap(
        spacing: 12,
        runSpacing: 12,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: <Widget>[
          SecondaryButton(
            onPressed: _isSaving ? null : _handleReset,
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(BootstrapIcons.arrowCounterclockwise),
                SizedBox(width: 8),
                Text('Reset'),
              ],
            ),
          ),
          PrimaryButton(
            onPressed: _isSaving || !_isDirty ? null : _handleSave,
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(BootstrapIcons.save),
                SizedBox(width: 8),
                Text('Save'),
              ],
            ),
          ),
          if (_statusMessage != null)
            Text(
              _statusMessage!,
              style: statusStyle,
            ),
          if (_statusMessage == null)
            Text(
              _isSaving ? 'Saving...' : (_isDirty ? 'Unsaved changes' : ''),
              style: statusStyle,
            ),
        ],
      ),
    );
  }

  String? _normalizeText(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.title);

  final String title;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Text(
      title,
      style: TextStyle(
        fontSize: 15,
        fontWeight: FontWeight.w600,
        color: colorScheme.foreground,
      ),
    );
  }
}

class _ToggleRow extends StatelessWidget {
  const _ToggleRow({
    required this.title,
    required this.subtitle,
    required this.value,
    required this.onChanged,
  });

  final String title;
  final String subtitle;
  final bool value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: colorScheme.foreground,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: TextStyle(
                    fontSize: 12,
                    color: colorScheme.mutedForeground,
                  ),
                ),
              ],
            ),
          ),
          Switch(
            value: value,
            onChanged: onChanged,
          ),
        ],
      ),
    );
  }
}

class _LabeledTextField extends StatelessWidget {
  const _LabeledTextField({
    required this.label,
    required this.controller,
    required this.placeholder,
    required this.enabled,
    required this.onChanged,
    this.keyboardType,
  });

  final String label;
  final TextEditingController controller;
  final String placeholder;
  final bool enabled;
  final ValueChanged<String> onChanged;
  final TextInputType? keyboardType;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w500,
            color: colorScheme.foreground,
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: controller,
          enabled: enabled,
          keyboardType: keyboardType,
          placeholder: Text(placeholder),
          onChanged: onChanged,
        ),
      ],
    );
  }
}

class _KeyValueRow extends StatelessWidget {
  const _KeyValueRow({
    required this.label,
    required this.value,
  });

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          width: 110,
          child: Text(
            label,
            style: TextStyle(
              fontSize: 12,
              color: colorScheme.mutedForeground,
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            value,
            style: TextStyle(
              fontSize: 13,
              color: colorScheme.foreground,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
      ],
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({
    required this.label,
    required this.accent,
  });

  final String label;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        border: Border.all(color: accent.withValues(alpha: 0.35)),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w500,
          color: accent,
        ),
      ),
    );
  }
}
