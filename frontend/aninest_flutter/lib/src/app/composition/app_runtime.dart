import 'package:aninest_flutter/src/app/coordination/app_selection_resolver.dart';
import 'package:aninest_flutter/src/app/coordination/backend_connection_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/host_event_coordinator.dart';
import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:aninest_flutter/src/app/workflows/app_bootstrap_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/library_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/player_workflow.dart';
import 'package:aninest_flutter/src/app/workflows/settings_workflow.dart';

class AppRuntime {
  AppRuntime({
    required this.actionState,
    required this.selectionResolver,
    required this.libraryMetadataSelectionSync,
    required this.hostEventCoordinator,
    required this.backendConnectionCoordinator,
    required this.appBootstrapWorkflow,
    required this.libraryWorkflow,
    required this.playerWorkflow,
    required this.settingsWorkflow,
  });

  final AppActionState actionState;
  final AppSelectionResolver selectionResolver;
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
