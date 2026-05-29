import 'package:flutter/widgets.dart';

class PlayerFocusController {
  PlayerFocusController()
    : focusNode = FocusNode(debugLabel: 'PlayerFocusScope');

  final FocusNode focusNode;

  bool get hasFocus => focusNode.hasFocus;

  void requestPrimaryFocus() {
    focusNode.requestFocus();
  }

  void releaseFocus() {
    focusNode.unfocus();
  }

  void dispose() {
    focusNode.dispose();
  }
}
