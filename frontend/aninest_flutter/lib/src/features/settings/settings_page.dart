import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:flutter/material.dart';

class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key, required this.controller});

  final AppController controller;

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  final TextEditingController _rateController = TextEditingController();
  final TextEditingController _volumeController = TextEditingController();
  bool _resumePlayback = true;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final player = widget.controller.appSettings?.player;
    if (player != null) {
      _rateController.text = player.preferredRate.toString();
      _volumeController.text = player.preferredVolume.toString();
      _resumePlayback = player.resumePlayback;
    }
  }

  @override
  void dispose() {
    _rateController.dispose();
    _volumeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        final settings = widget.controller.appSettings;
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Expanded(
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: settings == null
                        ? const Text('Settings have not loaded yet.')
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Player Settings',
                                style: Theme.of(context).textTheme.titleMedium,
                              ),
                              const SizedBox(height: 12),
                              TextField(
                                controller: _rateController,
                                decoration: const InputDecoration(
                                  labelText: 'Preferred Rate',
                                  border: OutlineInputBorder(),
                                ),
                              ),
                              const SizedBox(height: 12),
                              TextField(
                                controller: _volumeController,
                                decoration: const InputDecoration(
                                  labelText: 'Preferred Volume',
                                  border: OutlineInputBorder(),
                                ),
                              ),
                              const SizedBox(height: 12),
                              SwitchListTile(
                                value: _resumePlayback,
                                onChanged: (value) =>
                                    setState(() => _resumePlayback = value),
                                title: const Text('Resume Playback'),
                              ),
                              const SizedBox(height: 12),
                              FilledButton(
                                onPressed: () {
                                  final dto = PlayerSettingsDto(
                                    preferredRate:
                                        double.tryParse(_rateController.text) ??
                                        settings.player.preferredRate,
                                    preferredVolume:
                                        int.tryParse(_volumeController.text) ??
                                        settings.player.preferredVolume,
                                    resumePlayback: _resumePlayback,
                                  );
                                  widget.controller.savePlayerSettings(dto);
                                },
                                child: const Text('Save Player Settings'),
                              ),
                            ],
                          ),
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: settings == null
                        ? const Text('Settings summary unavailable.')
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Current Backend Settings',
                                style: Theme.of(context).textTheme.titleMedium,
                              ),
                              const SizedBox(height: 12),
                              Text('Rate: ${settings.player.preferredRate}'),
                              Text(
                                'Volume: ${settings.player.preferredVolume}',
                              ),
                              Text('Resume: ${settings.player.resumePlayback}'),
                              const Divider(height: 24),
                              Text(
                                'Auto scrape metadata: ${settings.metadata.autoScrapeMetadata}',
                              ),
                              Text(
                                'Thumbnail expiry days: ${settings.thumbnails.expiryDays}',
                              ),
                              Text(
                                'Generate on import: ${settings.thumbnails.generateOnImport}',
                              ),
                            ],
                          ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
