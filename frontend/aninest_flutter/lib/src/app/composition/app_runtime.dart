import 'package:aninest_flutter/src/app/coordination/backend_connection_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/host_event_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:aninest_flutter/src/app/workflows/app_bootstrap_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/library_add_folder_failure_result.dart';
import 'package:aninest_flutter/src/app/workflows/library_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/player_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/settings_workflow.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';

import 'app_dependencies.dart';

class AppRuntime {
  AppRuntime._({
    required this.actionState,
    required this.libraryMetadataSelectionSync,
    required this.hostEventCoordinator,
    required this.backendConnectionCoordinator,
    required this.appBootstrapWorkflow,
    required this.libraryWorkflow,
    required this.playerWorkflow,
    required this.settingsWorkflow,
  });

  factory AppRuntime.create({
    String? launchBaseUrl,
    required AppPreferences appPreferences,
    required AppDependencies dependencies,
  }) {
    final actionState = AppActionState();
    late final LibraryMetadataSelectionSync libraryMetadataSelectionSync;
    late final HostEventCoordinator hostEventCoordinator;
    late final AppBootstrapWorkflow appBootstrapWorkflow;

    Future<void> runOperation(
      Future<void> Function() operation, {
      bool showSpinner = true,
    }) {
      return actionState.run(operation, showSpinner: showSpinner);
    }

    libraryMetadataSelectionSync = LibraryMetadataSelectionSync(
      library: dependencies.library,
      metadata: dependencies.metadata,
    );
    hostEventCoordinator = HostEventCoordinator(
      hostEventService: dependencies.hostEventService,
      library: dependencies.library,
      metadata: dependencies.metadata,
      selectedFolderId: () =>
          dependencies.library.selectedFolderId ??
          dependencies.player.selectedFolderId ??
          (dependencies.library.folders.isNotEmpty
              ? dependencies.library.folders.first.folderId
              : null),
      refreshLibrary: () => runOperation(() async {
        await libraryMetadataSelectionSync.runWithSelectionRefreshSuspended(
          dependencies.library.refresh,
        );
        await libraryMetadataSelectionSync.refreshForCurrentSelection(
          force: true,
        );
      }, showSpinner: false),
      refreshMetadataForSelection: ({bool force = false}) =>
          libraryMetadataSelectionSync.refreshForCurrentSelection(force: force),
    );
    appBootstrapWorkflow = AppBootstrapWorkflow(
      hostEventCoordinator: hostEventCoordinator,
      libraryMetadataSelectionSync: libraryMetadataSelectionSync,
      loadSettings: dependencies.settings.load,
      refreshLibrary: dependencies.library.refresh,
      restorePlayer: dependencies.player.restore,
      runOperation: runOperation,
      readLastError: () => actionState.lastError,
    );

    return AppRuntime._(
      actionState: actionState,
      libraryMetadataSelectionSync: libraryMetadataSelectionSync,
      hostEventCoordinator: hostEventCoordinator,
      backendConnectionCoordinator: BackendConnectionCoordinator(
        launchBaseUrl: launchBaseUrl,
        appPreferences: appPreferences,
        client: dependencies.client,
        reloadFromBackend: ({bool restartHostEvents = false}) =>
            appBootstrapWorkflow.reloadFromBackend(
              restartHostEvents: restartHostEvents,
            ),
      ),
      appBootstrapWorkflow: appBootstrapWorkflow,
      libraryWorkflow: LibraryWorkflow(
        library: dependencies.library,
        libraryApi: dependencies.libraryApi,
        runOperation: runOperation,
        runWithSelectionRefreshSuspended:
            libraryMetadataSelectionSync.runWithSelectionRefreshSuspended,
        refreshMetadataForSelection: ({bool force = false}) =>
            libraryMetadataSelectionSync.refreshForCurrentSelection(
              force: force,
            ),
        buildAddFolderFailureResult: (path, errorMessage) =>
            buildLibraryAddFolderFailureResult(
              path,
              errorMessage,
              baseUrl: dependencies.client.baseUrl,
            ),
        readLastError: () => actionState.lastError,
      ),
      playerWorkflow: PlayerWorkflow(
        player: dependencies.player,
        runOperation: runOperation,
        refreshMetadataForSelection: ({bool force = false}) =>
            libraryMetadataSelectionSync.refreshForCurrentSelection(
              force: force,
            ),
        readLastError: () => actionState.lastError,
      ),
      settingsWorkflow: SettingsWorkflow(
        settings: dependencies.settings,
        runOperation: runOperation,
      ),
    );
  }

  final AppActionState actionState;
  final LibraryMetadataSelectionSync libraryMetadataSelectionSync;
  final HostEventCoordinator hostEventCoordinator;
  final BackendConnectionCoordinator backendConnectionCoordinator;
  final AppBootstrapWorkflow appBootstrapWorkflow;
  final LibraryWorkflow libraryWorkflow;
  final PlayerWorkflow playerWorkflow;
  final SettingsWorkflow settingsWorkflow;

  void dispose() {
    actionState.dispose();
    libraryMetadataSelectionSync.dispose();
    hostEventCoordinator.dispose();
  }
}
