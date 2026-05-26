import 'package:flutter/widgets.dart';

class WindowStateSnapshot {
  const WindowStateSnapshot({
    required this.width,
    required this.height,
    required this.isMaximized,
    this.left,
    this.top,
  });

  final double width;
  final double height;
  final bool isMaximized;
  final double? left;
  final double? top;

  Size get size => Size(width, height);

  Rect? get bounds {
    final left = this.left;
    final top = this.top;
    if (left == null || top == null) {
      return null;
    }

    return Rect.fromLTWH(left, top, width, height);
  }

  factory WindowStateSnapshot.fromBounds(
    Rect bounds, {
    required bool isMaximized,
  }) {
    return WindowStateSnapshot(
      width: bounds.width,
      height: bounds.height,
      isMaximized: isMaximized,
      left: bounds.left,
      top: bounds.top,
    );
  }

  factory WindowStateSnapshot.fromJson(Map<String, dynamic> json) {
    final width = (json['width'] as num?)?.toDouble();
    final height = (json['height'] as num?)?.toDouble();
    if (width == null || height == null || width <= 0 || height <= 0) {
      throw const FormatException('Invalid window size.');
    }

    return WindowStateSnapshot(
      width: width,
      height: height,
      isMaximized: json['isMaximized'] as bool? ?? false,
      left: (json['left'] as num?)?.toDouble(),
      top: (json['top'] as num?)?.toDouble(),
    );
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'width': width,
      'height': height,
      'isMaximized': isMaximized,
      'left': left,
      'top': top,
    };
  }
}
