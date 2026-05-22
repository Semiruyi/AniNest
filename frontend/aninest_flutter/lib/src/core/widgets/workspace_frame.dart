import 'package:shadcn_flutter/shadcn_flutter.dart';

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
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      children: <Widget>[
        Expanded(
          child: Row(
            children: <Widget>[
              if (sidebar != null)
                SizedBox(
                  width: sidebarWidth,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: colorScheme.card,
                      border: Border(
                        right: BorderSide(
                          color: colorScheme.border,
                        ),
                      ),
                    ),
                    child: sidebar,
                  ),
                ),
              Expanded(
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: colorScheme.background,
                  ),
                  child: content,
                ),
              ),
              if (inspector != null)
                SizedBox(
                  width: inspectorWidth,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: colorScheme.card,
                      border: Border(
                        left: BorderSide(
                          color: colorScheme.border,
                        ),
                      ),
                    ),
                    child: inspector,
                  ),
                ),
            ],
          ),
        ),
        if (bottomPane != null)
          SizedBox(
            height: bottomPaneHeight,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: colorScheme.card,
                border: Border(
                  top: BorderSide(
                    color: colorScheme.border,
                  ),
                ),
              ),
              child: bottomPane,
            ),
          ),
      ],
    );
  }
}
