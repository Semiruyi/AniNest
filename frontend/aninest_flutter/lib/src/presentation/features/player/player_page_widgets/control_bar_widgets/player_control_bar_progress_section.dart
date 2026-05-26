import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerControlBarProgressSection extends StatefulWidget {
  const PlayerControlBarProgressSection({super.key});

  @override
  State<PlayerControlBarProgressSection> createState() =>
      _PlayerControlBarProgressSectionState();
}

class _PlayerControlBarProgressSectionState
    extends State<PlayerControlBarProgressSection> {
  SliderValue _value = const SliderValue.single(0.5);

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(6),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 12),
      alignment: Alignment.center,
      child: Slider(
        value: _value,
        onChanged: (SliderValue value) {
          setState(() {
            _value = value;
          });
        },
      ),
    );
  }
}
