import 'package:flutter/widgets.dart';

typedef AnimatedPageStackBuilder =
    Widget Function(
      BuildContext context,
      Animation<double> animation,
      Widget? child,
    );

class AnimatedPageStack extends StatefulWidget {
  const AnimatedPageStack({
    super.key,
    required this.index,
    required this.children,
    required this.duration,
    required this.curve,
    this.onPresentedIndexChanged,
    this.alignment = AlignmentDirectional.topStart,
    this.textDirection,
    this.clipBehavior = Clip.hardEdge,
    this.sizing = StackFit.loose,
    this.animationBuilder,
  });

  final int index;
  final List<Widget> children;
  final Duration duration;
  final Curve curve;
  final ValueChanged<int>? onPresentedIndexChanged;
  final AlignmentGeometry alignment;
  final TextDirection? textDirection;
  final Clip clipBehavior;
  final StackFit sizing;
  final AnimatedPageStackBuilder? animationBuilder;

  @override
  State<AnimatedPageStack> createState() => _AnimatedPageStackState();
}

class _AnimatedPageStackState extends State<AnimatedPageStack>
    with SingleTickerProviderStateMixin {
  late int _presentedIndex = widget.index;
  late final AnimationController _animationController;
  late Animation<double> _animation;

  @override
  void initState() {
    super.initState();
    _animationController = AnimationController(
      vsync: this,
      duration: widget.duration,
    );
    _animation = _buildAnimation(widget.curve);
  }

  @override
  void didUpdateWidget(covariant AnimatedPageStack oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.duration != widget.duration) {
      _animationController.duration = widget.duration;
    }

    if (oldWidget.curve != widget.curve) {
      _animation = _buildAnimation(widget.curve);
    }

    if (oldWidget.index != widget.index) {
      _runTransition();
    }
  }

  @override
  void dispose() {
    _animationController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _animation,
      builder: (BuildContext context, Widget? child) {
        final builder = widget.animationBuilder ?? _defaultAnimationBuilder;
        return builder(context, _animation, child);
      },
      child: IndexedStack(
        alignment: widget.alignment,
        clipBehavior: widget.clipBehavior,
        sizing: widget.sizing,
        textDirection: widget.textDirection,
        index: _presentedIndex,
        children: widget.children,
      ),
    );
  }

  Animation<double> _buildAnimation(Curve curve) {
    return Tween<double>(begin: 1, end: 0).animate(
      CurvedAnimation(parent: _animationController, curve: curve),
    );
  }

  Future<void> _runTransition() async {
    if (_animationController.isAnimating) {
      _animationController.stop();
    }

    await _animationController.animateTo(1);
    if (!mounted) {
      return;
    }

    setState(() {
      _presentedIndex = widget.index;
    });
    widget.onPresentedIndexChanged?.call(_presentedIndex);

    await _animationController.animateTo(0);
  }

  static Widget _defaultAnimationBuilder(
    BuildContext context,
    Animation<double> animation,
    Widget? child,
  ) {
    return Opacity(
      opacity: animation.value,
      child: Transform.translate(
        offset: Offset(0, (1 - animation.value) * 10),
        child: child,
      ),
    );
  }
}
