/// Strips HTML tags and decodes a handful of common entities to render
/// server-authored rich-HTML content (news bodies, etc.) as plain text.
///
/// The web renders this HTML directly via sanitized `v-html`. Doing the same
/// in Flutter would need a full HTML-rendering package (e.g. `flutter_html`)
/// just for this one use — a real dependency-weight/complexity tradeoff not
/// justified for a handful of feed screens, so this is a deliberate,
/// documented simplification: content displays as readable plain text
/// instead of rendered rich HTML (no images, tables, or inline styling).
String stripHtmlToText(String html) {
  final withoutTags = html.replaceAll(RegExp(r'<[^>]*>'), ' ');
  final decoded = withoutTags
      .replaceAll('&nbsp;', ' ')
      .replaceAll('&amp;', '&')
      .replaceAll('&lt;', '<')
      .replaceAll('&gt;', '>')
      .replaceAll('&quot;', '"')
      .replaceAll('&#39;', "'");
  return decoded.replaceAll(RegExp(r'\s+'), ' ').trim();
}
