enum AppFeedbackKind { toastInfo, dialogError }

class AppFeedbackRequest {
  const AppFeedbackRequest({
    required this.kind,
    required this.title,
    required this.message,
  });

  final AppFeedbackKind kind;
  final String title;
  final String message;
}
