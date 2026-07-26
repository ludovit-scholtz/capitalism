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

  // App / About nav
  static const about = FontAwesomeIcons.circleInfo;
  static const devInfo = FontAwesomeIcons.bug;
  static const copy = FontAwesomeIcons.copy;
  static const trash = FontAwesomeIcons.trashCan;

  // Servers / bounties
  static const server = FontAwesomeIcons.server;
  static const bounty = FontAwesomeIcons.medal;

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
  static const chevronDown = FontAwesomeIcons.chevronDown;
  static const city = FontAwesomeIcons.locationDot;
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
  static const wallet = FontAwesomeIcons.wallet;

  // Building unit types (dashboard supply-chain strip)
  static const unitPurchase = FontAwesomeIcons.cartShopping;
  static const unitMining = FontAwesomeIcons.mound;
  static const unitManufacturing = FontAwesomeIcons.gear;
  static const unitStorage = FontAwesomeIcons.boxesStacked;
  static const unitB2bSales = FontAwesomeIcons.handshake;
  static const unitPublicSales = FontAwesomeIcons.tag;
  static const unitBranding = FontAwesomeIcons.palette;
  static const unitMarketing = FontAwesomeIcons.bullhorn;
  static const unitProductQuality = FontAwesomeIcons.flask;
  static const unitBrandQuality = FontAwesomeIcons.star;
}
