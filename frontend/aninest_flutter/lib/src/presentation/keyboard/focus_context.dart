import 'package:flutter/widgets.dart';

final class FocusContext {
  FocusContext._();

  static bool hasEditableTextFocus() {
    final focusedContext = FocusManager.instance.primaryFocus?.context;
    if (focusedContext == null) {
      return false;
    }

    return focusedContext.widget is EditableText;
  }
}
