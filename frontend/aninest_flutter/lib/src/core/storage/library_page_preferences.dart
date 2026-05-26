class LibraryPagePreferences {
  const LibraryPagePreferences({this.leftPaneWidth, this.rightPaneWidth});

  final double? leftPaneWidth;
  final double? rightPaneWidth;

  factory LibraryPagePreferences.fromJson(Map<String, dynamic> json) {
    return LibraryPagePreferences(
      leftPaneWidth: (json['leftPaneWidth'] as num?)?.toDouble(),
      rightPaneWidth: (json['rightPaneWidth'] as num?)?.toDouble(),
    );
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'leftPaneWidth': leftPaneWidth,
      'rightPaneWidth': rightPaneWidth,
    };
  }
}
