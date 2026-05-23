import 'package:file_selector/file_selector.dart';

class DirectoryPicker {
  const DirectoryPicker();

  Future<String?> pickDirectory() => getDirectoryPath();
}
