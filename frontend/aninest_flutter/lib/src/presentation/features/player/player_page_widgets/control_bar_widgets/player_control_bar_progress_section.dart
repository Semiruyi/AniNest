import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerControlBarProgressSection extends StatelessWidget {
  const PlayerControlBarProgressSection({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF9F1239),
        borderRadius: BorderRadius.circular(6),
      ),
      child: const Padding(
        padding: EdgeInsets.symmetric(horizontal: 12),
        child: Align(
          alignment: Alignment.centerLeft,
          child: Text(
            'Progress Section',
            style: TextStyle(color: Color(0xFFF8FAFC), fontSize: 12),
          ),
        ),
      ).semiBold(),
    );
  }
}
