import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_state.dart';
import 'package:flutter/widgets.dart';

typedef PlayerSelectorValueBuilder<T> =
    Widget Function(BuildContext context, T value);
typedef PlayerSelectorEquals<T> = bool Function(T previous, T next);

class PlayerSelector<T> extends StatefulWidget {
  const PlayerSelector({
    super.key,
    required this.controller,
    required this.selector,
    required this.builder,
    this.equals,
  });

  final PlayerController controller;
  final T Function(PlayerState state) selector;
  final PlayerSelectorValueBuilder<T> builder;
  final PlayerSelectorEquals<T>? equals;

  @override
  State<PlayerSelector<T>> createState() => _PlayerSelectorState<T>();
}

class _PlayerSelectorState<T> extends State<PlayerSelector<T>> {
  late T _selectedValue;

  @override
  void initState() {
    super.initState();
    _selectedValue = widget.selector(widget.controller.state);
    widget.controller.addListener(_handleControllerChanged);
  }

  @override
  void didUpdateWidget(PlayerSelector<T> oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.controller != widget.controller) {
      oldWidget.controller.removeListener(_handleControllerChanged);
      widget.controller.addListener(_handleControllerChanged);
    }

    final nextSelectedValue = widget.selector(widget.controller.state);
    if (!_isEqual(_selectedValue, nextSelectedValue)) {
      _selectedValue = nextSelectedValue;
    }
  }

  @override
  void dispose() {
    widget.controller.removeListener(_handleControllerChanged);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return widget.builder(context, _selectedValue);
  }

  void _handleControllerChanged() {
    final nextSelectedValue = widget.selector(widget.controller.state);
    if (_isEqual(_selectedValue, nextSelectedValue)) {
      return;
    }

    setState(() {
      _selectedValue = nextSelectedValue;
    });
  }

  bool _isEqual(T previous, T next) {
    final equals = widget.equals;
    return equals != null ? equals(previous, next) : previous == next;
  }
}
