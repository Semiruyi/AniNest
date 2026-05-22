import 'package:aninest_flutter/src/app/aninest_app.dart';
import 'package:flutter/widgets.dart';
import 'package:media_kit/media_kit.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  MediaKit.ensureInitialized();
  runApp(const AniNestApp());
}
