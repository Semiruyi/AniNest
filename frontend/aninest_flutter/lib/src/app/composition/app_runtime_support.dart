import 'package:aninest_flutter/src/app/coordination/library_metadata_selection_sync.dart';
import 'package:aninest_flutter/src/app/state/app_action_state.dart';
import 'package:aninest_flutter/src/app/workflows/library_add_folder_failure_result.dart';
import 'package:aninest_flutter/src/models/library_models.dart';

typedef RunAppRuntimeOperation =
    Future<void> Function(
      Future<void> Function() operation, {
      bool showSpinner,
    });

RunAppRuntimeOperation createAppRuntimeOperationRunner(
  AppActionState actionState,
) {
  return (Future<void> Function() operation, {bool showSpinner = true}) {
    return actionState.run(operation, showSpinner: showSpinner);
  };
}

Future<void> refreshLibraryFromHostEvent({
  required RunAppRuntimeOperation runOperation,
  required LibraryMetadataSelectionSync libraryMetadataSelectionSync,
  required Future<void> Function() refreshLibrary,
}) {
  return runOperation(() async {
    await libraryMetadataSelectionSync.runWithSelectionRefreshSuspended(
      refreshLibrary,
    );
    await libraryMetadataSelectionSync.refreshForCurrentSelection(force: true);
  }, showSpinner: false);
}

AddLibraryFolderResultDto buildAddFolderFailureResultForBaseUrl(
  String path,
  String errorMessage, {
  required String baseUrl,
}) {
  return buildLibraryAddFolderFailureResult(
    path,
    errorMessage,
    baseUrl: baseUrl,
  );
}
