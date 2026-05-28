import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/services/host_event_service.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';

class AppDependencies {
  AppDependencies._({
    required this.appPreferences,
    required this.client,
    required this.libraryApi,
    required this.sessionApi,
    required this.playlistApi,
    required this.settingsApi,
    required this.metadataApi,
    required this.thumbnailApi,
    required this.hostEventService,
    required this.library,
    required this.player,
    required this.settings,
    required this.metadata,
  });

  factory AppDependencies.create({
    required AppPreferences appPreferences,
    required String initialBaseUrl,
  }) {
    final client = AniNestHttpClient(baseUrl: initialBaseUrl);
    final libraryApi = LibraryApi(client);
    final sessionApi = SessionApi(client);
    final playlistApi = PlaylistApi(client);
    final settingsApi = SettingsApi(client);
    final metadataApi = MetadataApi(client);
    final thumbnailApi = ThumbnailApi(client);

    return AppDependencies._(
      appPreferences: appPreferences,
      client: client,
      libraryApi: libraryApi,
      sessionApi: sessionApi,
      playlistApi: playlistApi,
      settingsApi: settingsApi,
      metadataApi: metadataApi,
      thumbnailApi: thumbnailApi,
      hostEventService: HostEventService(client),
      library: LibraryController(libraryApi),
      player: PlayerController(sessionApi, playlistApi, appPreferences),
      settings: SettingsController(settingsApi, appPreferences),
      metadata: MetadataController(metadataApi, thumbnailApi),
    );
  }

  final AppPreferences appPreferences;
  final AniNestHttpClient client;

  final LibraryApi libraryApi;
  final SessionApi sessionApi;
  final PlaylistApi playlistApi;
  final SettingsApi settingsApi;
  final MetadataApi metadataApi;
  final ThumbnailApi thumbnailApi;
  final HostEventService hostEventService;

  final LibraryController library;
  final PlayerController player;
  final SettingsController settings;
  final MetadataController metadata;

  void dispose() {
    library.dispose();
    player.dispose();
    settings.dispose();
    metadata.dispose();
    client.close();
  }
}
