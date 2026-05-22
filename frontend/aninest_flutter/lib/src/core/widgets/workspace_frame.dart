import 'package:flutter/material.dart';

class WorkspaceFrame extends StatelessWidget {
  const WorkspaceFrame({
    super.key,
    required this.content,
    this.sidebar,
    this.inspector,
    this.bottomPane,
    this.sidebarWidth = 280,
    this.inspectorWidth = 320,
    this.bottomPaneHeight = 220,
  });

  final Widget content;
  final Widget? sidebar;
  final Widget? inspector;
  final Widget? bottomPane;
  final double sidebarWidth;
  final double inspectorWidth;
  final double bottomPaneHeight;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Expanded(
          child: Row(
            children: <Widget>[
              if (sidebar != null)
                SizedBox(
                  width: sidebarWidth,
                  child: sidebar,
                ),
              Expanded(
                child: content,
              ),
              if (inspector != null)
                SizedBox(
                  width: inspectorWidth,
                  child: inspector,
                ),
            ],
          ),
        ),
        if (bottomPane != null)
          SizedBox(
            height: bottomPaneHeight,
            child: bottomPane,
          ),
      ],
    );
  }
}
