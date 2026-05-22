import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/features/library/library_page.dart';
import 'package:aninest_flutter/src/features/metadata/metadata_page.dart';
import 'package:aninest_flutter/src/features/player/player_page.dart';
import 'package:aninest_flutter/src/features/settings/settings_page.dart';
import 'package:flutter/material.dart';

class AppShell extends StatefulWidget {
  const AppShell({super.key, required this.controller});

  final AppController controller;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _index = 0;
  late final TextEditingController _baseUrlController;

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

  @override
  Widget build(BuildContext context) {
    final pages = <Widget>[
      LibraryPage(controller: widget.controller),
      PlayerPage(controller: widget.controller),
      MetadataPage(controller: widget.controller),
      SettingsPage(controller: widget.controller),
    ];

    return AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        return Scaffold(
          appBar: AppBar(
            title: const Text('AniNest Flutter Shell'),
            actions: [
              SizedBox(
                width: 320,
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: TextField(
                    controller: _baseUrlController,
                    decoration: const InputDecoration(
                      hintText: 'Backend Base URL',
                      border: OutlineInputBorder(),
                      contentPadding: EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 10,
                      ),
                    ),
                    onSubmitted: (value) =>
                        widget.controller.updateBaseUrl(value),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              IconButton(
                tooltip: 'Reconnect',
                onPressed: () =>
                    widget.controller.updateBaseUrl(_baseUrlController.text),
                icon: const Icon(Icons.link),
              ),
              IconButton(
                tooltip: 'Refresh all',
                onPressed: widget.controller.bootstrap,
                icon: const Icon(Icons.refresh),
              ),
              const SizedBox(width: 12),
            ],
            bottom: PreferredSize(
              preferredSize: const Size.fromHeight(32),
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
                alignment: Alignment.centerLeft,
                child: Row(
                  children: [
                    Icon(
                      widget.controller.lastError == null
                          ? Icons.check_circle
                          : Icons.error,
                      size: 16,
                      color: widget.controller.lastError == null
                          ? Colors.greenAccent
                          : Colors.redAccent,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        widget.controller.lastError ??
                            'Connected to ${widget.controller.baseUrl}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (widget.controller.isLoading)
                      const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                  ],
                ),
              ),
            ),
          ),
          body: pages[_index],
          bottomNavigationBar: NavigationBar(
            selectedIndex: _index,
            onDestinationSelected: (value) => setState(() => _index = value),
            destinations: const [
              NavigationDestination(
                icon: Icon(Icons.video_library_outlined),
                label: 'Library',
              ),
              NavigationDestination(
                icon: Icon(Icons.play_circle_outline),
                label: 'Player',
              ),
              NavigationDestination(
                icon: Icon(Icons.auto_awesome_outlined),
                label: 'Metadata',
              ),
              NavigationDestination(
                icon: Icon(Icons.settings_outlined),
                label: 'Settings',
              ),
            ],
          ),
        );
      },
    );
  }
}
