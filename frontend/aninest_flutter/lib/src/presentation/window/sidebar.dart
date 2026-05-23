import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class Sidebar extends StatelessWidget {
  const Sidebar({super.key, this.width = 280});

  final double width;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final l10n = AppLocalizations.of(context);

    return SizedBox(
      width: width,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: colorScheme.card,
          border: Border(right: BorderSide(color: colorScheme.border)),
        ),
        child: Text(l10n.sidebarPlaceholder),
      ),
    );
  }
}
