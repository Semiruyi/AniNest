import 'package:aninest_flutter/src/features/player/application/player_controller.dart';

typedef RunPlayerOperation = Future<void> Function(
  Future<void> Function() operation, {
  bool showSpinner,
});
typedef RefreshSelectedMetadata = Future<void> Function({bool force});
typedef ReadOperationError = String? Function();

class PlayerWorkflow {
  PlayerWorkflow({
    required PlayerController player,
    required RunPlayerOperation runOperation,
    required RefreshSelectedMetadata refreshMetadataForSelection,
    required ReadOperationError readLastError,
  }) : _player = player,
       _runOperation = runOperation,
       _refreshMetadataForSelection = refreshMetadataForSelection,
       _readLastError = readLastError;

  final PlayerController _player;
  final RunPlayerOperation _runOperation;
  final RefreshSelectedMetadata _refreshMetadataForSelection;
  final ReadOperationError _readLastError;

  Future<void> openFolder(String folderId) async {
    await _runOperation(() async {
      await _player.openFolder(folderId);
      await _refreshMetadataForSelection(force: true);
    });
  }

  Future<String?> openLibraryFolder(String folderId) async {
    await _runOperation(() async {
      await _player.openFolder(folderId);
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
    return _readLastError();
  }

  Future<void> selectItem(String itemId) async {
    await _runOperation(() async {
      await _player.selectItem(itemId);
    }, showSpinner: false);
  }

  Future<void> moveNext() async {
    await _runOperation(() async {
      await _player.moveNext();
    }, showSpinner: false);
  }

  Future<void> movePrevious() async {
    await _runOperation(() async {
      await _player.movePrevious();
    }, showSpinner: false);
  }

  Future<void> closeSession() async {
    await _runOperation(() async {
      await _player.closeSession();
      await _refreshMetadataForSelection(force: true);
    }, showSpinner: false);
  }
}
