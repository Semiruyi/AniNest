import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_shell.dart';
import 'package:flutter/material.dart';

class AniNestApp extends StatefulWidget {
  const AniNestApp({super.key});

  @override
  State<AniNestApp> createState() => _AniNestAppState();
}

class _AniNestAppState extends State<AniNestApp> {
  late final AppController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AppController();
    _controller.bootstrap();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'AniNest',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(),
      home: AppShell(controller: _controller),
    );
  }
}
