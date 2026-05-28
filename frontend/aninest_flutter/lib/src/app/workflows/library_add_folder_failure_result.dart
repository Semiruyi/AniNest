import 'package:aninest_flutter/src/models/library_models.dart';

AddLibraryFolderResultDto buildLibraryAddFolderFailureResult(
  String path,
  String errorMessage, {
  required String baseUrl,
}) {
  final normalized = errorMessage.toLowerCase();
  if (normalized.contains('socketexception') ||
      normalized.contains('clientexception')) {
    return AddLibraryFolderResultDto(
      status: 'failed',
      message:
          'Unable to connect to the backend at $baseUrl. Please make sure AniNest.Host is running, then try again.',
      reasonCode: 'network_error',
      folder: null,
    );
  }

  return AddLibraryFolderResultDto(
    status: 'failed',
    message: 'Unable to add folder "$path". $errorMessage',
    reasonCode: 'unexpected_error',
    folder: null,
  );
}
