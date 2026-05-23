import 'dart:collection';

import 'package:aninest_flutter/src/presentation/feedback/app_feedback_models.dart';
import 'package:flutter/foundation.dart';

class AppFeedbackController extends ChangeNotifier {
  final Queue<AppFeedbackRequest> _pending = Queue<AppFeedbackRequest>();

  void publish(AppFeedbackRequest request) {
    _pending.addLast(request);
    notifyListeners();
  }

  AppFeedbackRequest? takeNext() {
    if (_pending.isEmpty) {
      return null;
    }

    return _pending.removeFirst();
  }
}
