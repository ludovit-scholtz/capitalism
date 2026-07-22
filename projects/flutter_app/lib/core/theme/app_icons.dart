import 'package:font_awesome_flutter/font_awesome_flutter.dart';

/// Central Font Awesome icon mapping for the whole app — the mobile
/// counterpart of `projects/frontend/src/lib/fontAwesomeIcons.ts`. Screens
/// should reference `AppIcons.xxx` (used with the `FaIcon` widget) rather
/// than calling `FontAwesomeIcons.xxx` or `Icons.xxx` directly, so the icon
/// vocabulary stays centralized and auditable in one place, matching the
/// web app's icon set wherever the same concept appears there.
class AppIcons {
  AppIcons._();

  // Brand / chrome
  static const brandMark = FontAwesomeIcons.coins;

  // Main nav
  static const home = FontAwesomeIcons.house;
  static const dashboard = FontAwesomeIcons.gaugeHigh;
  static const leaderboard = FontAwesomeIcons.trophy;
  static const cities = FontAwesomeIcons.city;
  static const news = FontAwesomeIcons.newspaper;
  static const tutorial = FontAwesomeIcons.graduationCap;

  // Economy nav
  static const exchange = FontAwesomeIcons.rightLeft;
  static const stocks = FontAwesomeIcons.chartLine;
  static const forex = FontAwesomeIcons.moneyBillTransfer;
  static const contracts = FontAwesomeIcons.fileInvoiceDollar;
  static const banking = FontAwesomeIcons.landmark;
  static const bankStatement = FontAwesomeIcons.receipt;
  static const campaigns = FontAwesomeIcons.bullhorn;
  static const marketDashboard = FontAwesomeIcons.store;
  static const energy = FontAwesomeIcons.bolt;
  static const tradeRoutes = FontAwesomeIcons.route;

  // Build nav
  static const buildingMarket = FontAwesomeIcons.building;
  static const encyclopedia = FontAwesomeIcons.bookOpen;

  // Social nav
  static const chat = FontAwesomeIcons.comments;
  static const discord = FontAwesomeIcons.discord;

  // Administration nav
  static const operations = FontAwesomeIcons.shieldHalved;

  // Auth
  static const login = FontAwesomeIcons.rightToBracket;
  static const logout = FontAwesomeIcons.rightFromBracket;
  static const errorOutline = FontAwesomeIcons.triangleExclamation;
  static const lock = FontAwesomeIcons.lock;
  static const lockOpen = FontAwesomeIcons.lockOpen;

  // Common UI
  static const close = FontAwesomeIcons.xmark;
  static const check = FontAwesomeIcons.check;
  static const checkCircle = FontAwesomeIcons.circleCheck;
  static const warning = FontAwesomeIcons.circleExclamation;
  static const search = FontAwesomeIcons.magnifyingGlass;
  static const chevronRight = FontAwesomeIcons.chevronRight;
  static const chevronLeft = FontAwesomeIcons.chevronLeft;
  static const arrowRight = FontAwesomeIcons.arrowRight;
  static const arrowUp = FontAwesomeIcons.arrowUp;
  static const radioChecked = FontAwesomeIcons.circleDot;
  static const radioUnchecked = FontAwesomeIcons.circle;
  static const clock = FontAwesomeIcons.clock;
  static const bell = FontAwesomeIcons.bell;
  static const wifiOff = FontAwesomeIcons.wifi;
  static const construction = FontAwesomeIcons.helmetSafety;
  static const celebration = FontAwesomeIcons.champagneGlasses;

  // Economy / game domain
  static const factory = FontAwesomeIcons.industry;
  static const business = FontAwesomeIcons.briefcase;
  static const sell = FontAwesomeIcons.tag;
  static const upgrade = FontAwesomeIcons.arrowUp;
}
