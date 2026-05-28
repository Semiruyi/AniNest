import 'package:aninest_flutter/src/app/coordination/app_selection_resolver.dart';
import 'package:aninest_flutter/src/app/coordination/backend_connection_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/host_event_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:aninest_flutter/src/app/workflows/app_bootstrap_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/library_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/player_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/settings_workflow.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';

import 'app_dependencies.dart';
import 'app_runtime.dart';
import 'app_runtime_support.dart';

class AppRuntimeAssembler {
  const AppRuntimeAssembler();

  AppRuntime assemble({
    String? launchBaseUrl,
    required AppPreferences appPreferences,
    required AppDependencies dependencies,
  }) {
    final actionState = AppActionState();
    final runOperation = createAppRuntimeOperationRunner(actionState);
    final selectionResolver = AppSelectionResolver(
      readLibrarySelectedFolderId: () => dependencies.library.selectedFolderId,
      readPlayerSelectedFolderId: () => dependencies.player.selectedFolderId,
      readLibraryFolders: () => dependencies.library.folders,
      readSelectedItemId: () => dependencies.player.selectedItemId,
    );
    late final LibraryMetadataSelectionSync libraryMetadataSelectionSync;
    late final HostEventCoordinator hostEventCoordinator;
    late final AppBootstrapWorkflow appBootstrapWorkflow;

    libraryMetadataSelectionSync = LibraryMetadataSelectionSync(
      library: dependencies.library,
      metadata: dependencies.metadata,
    );
    hostEventCoordinator = HostEventCoordinator(
      hostEventService: dependencies.hostEventService,
      library: dependencies.library,
      metadata: dependencies.metadata,
      selectedFolderId: selectionResolver.resolveSelectedFolderId,
      refreshLibrary: () => refreshLibraryFromHostEvent(
        runOperation: runOperation,
        libraryMetadataSelectionSync: libraryMetadataSelectionSync,
        refreshLibrary: dependencies.library.refresh,
      ),
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

    return AppRuntime(
      actionState: actionState,
      selectionResolver: selectionResolver,
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
            buildAddFolderFailureResultForBaseUrl(
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
}
