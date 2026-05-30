namespace MasterApi.Utilities;

/// <summary>
/// The kind of legal document published by the service provider.
/// </summary>
public enum LegalDocumentKind
{
    Terms,
    Privacy,
}

/// <summary>A single titled section of a legal document.</summary>
public sealed record LegalDocumentSection(string Heading, IReadOnlyList<string> Paragraphs);

/// <summary>A complete legal document in a single locale.</summary>
public sealed record LegalDocument(
    LegalDocumentKind Kind,
    string Locale,
    string Title,
    string Version,
    string EffectiveDate,
    string Intro,
    IReadOnlyList<LegalDocumentSection> Sections);

/// <summary>
/// Canonical, single source of truth for the Terms &amp; Conditions and the Privacy Policy.
/// The same content is rendered on the master frontend, exposed via GraphQL, and attached to the
/// first registration email as a PDF. Documents are governed by the law of the Slovak Republic and
/// drafted in favour of the service provider, Scholtz &amp; Company, jsa (IČO 51882272).
/// </summary>
public static class LegalDocuments
{
    /// <summary>Document version, bump when the wording changes.</summary>
    public const string Version = "2026.1";

    /// <summary>Effective date of the current version (ISO 8601).</summary>
    public const string EffectiveDate = "2026-01-01";

    /// <summary>Tokenization platform whose terms apply to tokenized gold.</summary>
    public const string TokenizationPlatformTermsUrl = "https://asa.gold/terms/latest";

    public const string ProviderName = "Scholtz & Company, jsa";
    public const string ProviderRegistrationId = "51882272";

    public static IReadOnlyList<LegalDocument> All(string locale)
    {
        var normalized = EmailLocalizations.NormalizeLocale(locale);
        return [Get(LegalDocumentKind.Terms, normalized), Get(LegalDocumentKind.Privacy, normalized)];
    }

    public static LegalDocument Get(LegalDocumentKind kind, string locale)
    {
        var normalized = EmailLocalizations.NormalizeLocale(locale);
        return kind switch
        {
            LegalDocumentKind.Terms => Terms(normalized),
            LegalDocumentKind.Privacy => Privacy(normalized),
            _ => Terms(normalized),
        };
    }

    public static string FileName(LegalDocumentKind kind, string locale)
    {
        var normalized = EmailLocalizations.NormalizeLocale(locale);
        var slug = kind == LegalDocumentKind.Terms ? "terms-and-conditions" : "privacy-policy";
        return $"capitalism-{slug}-{normalized}.pdf";
    }

    private static LegalDocument Terms(string locale) => locale switch
    {
        "sk" => new LegalDocument(
            LegalDocumentKind.Terms,
            "sk",
            "Všeobecné obchodné podmienky",
            Version,
            EffectiveDate,
            $"Tieto Všeobecné obchodné podmienky (ďalej len „Podmienky“) upravujú používanie hry a platformy Capitalism, ktorú prevádzkuje poskytovateľ služby {ProviderName}, IČO {ProviderRegistrationId} (ďalej len „Poskytovateľ“). Poskytovateľ je poskytovateľom služby ASA.Gold. Registráciou alebo používaním služby s týmito Podmienkami v plnom rozsahu súhlasíte.",
            [
                new LegalDocumentSection("1. Poskytovateľ služby", [
                    $"Prevádzkovateľom a poskytovateľom služby je {ProviderName}, IČO {ProviderRegistrationId}, zapísaná v príslušnom obchodnom registri Slovenskej republiky.",
                    "Poskytovateľ vystupuje ako poskytovateľ služby ASA.Gold a sprostredkúva prístup k tokenizovanému zlatu prostredníctvom tokenizačnej platformy ASA.Gold.",
                ]),
                new LegalDocumentSection("2. Rozhodné právo a jurisdikcia", [
                    "Tieto Podmienky, ako aj všetky vzťahy z nich vyplývajúce, sa riadia právnym poriadkom Slovenskej republiky, najmä zákonom č. 40/1964 Zb. Občiansky zákonník, zákonom č. 22/2004 Z. z. o elektronickom obchode a príslušnými predpismi na ochranu spotrebiteľa.",
                    "Na riešenie sporov sú príslušné súdy Slovenskej republiky. Spotrebiteľ má právo obrátiť sa na subjekt alternatívneho riešenia sporov.",
                ]),
                new LegalDocumentSection("3. Predmet služby", [
                    "Capitalism je online ekonomická hra. Poskytovateľ poskytuje prístup k hre „tak ako je“ a „ako je dostupná“, bez záruky nepretržitej dostupnosti.",
                    "Herná mena, virtuálne predmety a herný postup nemajú peňažnú hodnotu a nie sú zameniteľné za peniaze, s výnimkou tokenizovaného zlata podľa článku 6.",
                ]),
                new LegalDocumentSection("4. Registrácia a používateľský účet", [
                    "Na používanie služby je potrebná registrácia. Používateľ je povinný uvádzať pravdivé a aktuálne údaje a chrániť svoje prihlasovacie údaje.",
                    "Službu môžu používať len osoby staršie ako 18 rokov, resp. osoby spôsobilé na právne úkony v plnom rozsahu.",
                ]),
                new LegalDocumentSection("5. Platby", [
                    "Platby je možné realizovať prostredníctvom blockchainu, služby PayPal, Stripe alebo Revolut. Platby spracúvajú príslušní poskytovatelia platobných služieb podľa svojich vlastných podmienok.",
                    "Všetky platby sú konečné. Vzhľadom na okamžité dodanie digitálneho obsahu používateľ súhlasí so začatím plnenia pred uplynutím lehoty na odstúpenie od zmluvy, čím v rozsahu poskytnutého plnenia zaniká právo na odstúpenie.",
                ]),
                new LegalDocumentSection("6. Mechanizmus pay-to-play a tokenizované zlato", [
                    "Mechanizmus „pay-to-play“ umožňuje hráčom získať tokenizované zlato. Tokenizované zlato je vydávané a spravované prostredníctvom tokenizačnej platformy ASA.Gold.",
                    $"Na tokenizované zlato sa vzťahujú podmienky tokenizačnej platformy ASA.Gold dostupné na {TokenizationPlatformTermsUrl}, s ktorými je používateľ povinný sa oboznámiť.",
                    "Na to, aby hráč mohol prijímať platby alebo výplaty v tokenizovanom zlate, sa môže vyžadovať overenie totožnosti (KYC) a kontrola podľa predpisov o predchádzaní legalizácii príjmov z trestnej činnosti (AML). Poskytovateľ je oprávnený požadovať predloženie dokladov totožnosti a pôvodu prostriedkov a do ich overenia výplatu pozdržať alebo zamietnuť.",
                    "Poskytovateľ je oprávnený odmietnuť alebo obmedziť výplatu, ak hráč neukončí proces KYC/AML, alebo ak existuje dôvodné podozrenie z porušenia zákona či týchto Podmienok.",
                ]),
                new LegalDocumentSection("7. Zakázané konanie", [
                    "Používateľ sa zaväzuje nevyužívať chyby, automatizované nástroje, podvody ani iné konanie poškodzujúce službu alebo ostatných používateľov.",
                    "Pri porušení je Poskytovateľ oprávnený účet okamžite a bez náhrady obmedziť alebo zrušiť.",
                ]),
                new LegalDocumentSection("8. Obmedzenie zodpovednosti", [
                    "V maximálnom rozsahu povolenom právom Poskytovateľ nezodpovedá za žiadnu nepriamu, následnú alebo náhodnú škodu, ušlý zisk, stratu dát ani stratu hernej hodnoty.",
                    "Celková zodpovednosť Poskytovateľa je obmedzená do výšky sumy, ktorú používateľ skutočne uhradil Poskytovateľovi za posledných dvanásť mesiacov.",
                    "Poskytovateľ nezodpovedá za výpadky, technické chyby, konanie tretích strán (vrátane poskytovateľov platobných služieb a blockchainových sietí) ani za zmeny hodnoty tokenizovaného zlata.",
                ]),
                new LegalDocumentSection("9. Zmeny podmienok a služby", [
                    "Poskytovateľ je oprávnený tieto Podmienky a službu kedykoľvek jednostranne zmeniť alebo ukončiť. O podstatných zmenách bude používateľ informovaný vhodným spôsobom.",
                    "Pokračovaním v používaní služby po nadobudnutí účinnosti zmien používateľ s týmito zmenami súhlasí.",
                ]),
                new LegalDocumentSection("10. Ukončenie", [
                    "Používateľ môže svoj účet kedykoľvek zrušiť. Poskytovateľ je oprávnený ukončiť poskytovanie služby alebo zrušiť účet, najmä pri porušení týchto Podmienok.",
                    "Ustanovenia, ktoré majú svojou povahou trvať aj po ukončení (najmä obmedzenie zodpovednosti), zostávajú v platnosti.",
                ]),
                new LegalDocumentSection("11. Záverečné ustanovenia", [
                    "Ak sa niektoré ustanovenie týchto Podmienok stane neplatným, ostatné ustanovenia zostávajú v platnosti.",
                    $"Otázky týkajúce sa týchto Podmienok je možné adresovať Poskytovateľovi {ProviderName}.",
                ]),
            ]),
        "de" => new LegalDocument(
            LegalDocumentKind.Terms,
            "de",
            "Allgemeine Geschäftsbedingungen",
            Version,
            EffectiveDate,
            $"Diese Allgemeinen Geschäftsbedingungen (nachfolgend „Bedingungen“) regeln die Nutzung des Spiels und der Plattform Capitalism, betrieben vom Dienstanbieter {ProviderName}, IČO {ProviderRegistrationId} (nachfolgend „Anbieter“). Der Anbieter ist ASA.Gold-Dienstanbieter. Mit der Registrierung oder Nutzung des Dienstes stimmen Sie diesen Bedingungen vollständig zu.",
            [
                new LegalDocumentSection("1. Dienstanbieter", [
                    $"Betreiber und Dienstanbieter ist {ProviderName}, IČO {ProviderRegistrationId}, eingetragen im zuständigen Handelsregister der Slowakischen Republik.",
                    "Der Anbieter handelt als ASA.Gold-Dienstanbieter und vermittelt den Zugang zu tokenisiertem Gold über die Tokenisierungsplattform ASA.Gold.",
                ]),
                new LegalDocumentSection("2. Anwendbares Recht und Gerichtsstand", [
                    "Diese Bedingungen und alle daraus entstehenden Beziehungen unterliegen dem Recht der Slowakischen Republik, insbesondere dem Gesetz Nr. 40/1964 Slg. (Bürgerliches Gesetzbuch), dem Gesetz Nr. 22/2004 Slg. über den elektronischen Handel sowie den geltenden Verbraucherschutzvorschriften.",
                    "Für Streitigkeiten sind die Gerichte der Slowakischen Republik zuständig. Verbraucher können sich an eine Stelle zur alternativen Streitbeilegung wenden.",
                ]),
                new LegalDocumentSection("3. Gegenstand des Dienstes", [
                    "Capitalism ist ein Online-Wirtschaftsspiel. Der Anbieter stellt den Zugang „wie besehen“ und „wie verfügbar“ bereit, ohne Gewähr für ununterbrochene Verfügbarkeit.",
                    "Spielwährung, virtuelle Gegenstände und Spielfortschritt haben keinen Geldwert und sind nicht in Geld umtauschbar, mit Ausnahme von tokenisiertem Gold gemäß Artikel 6.",
                ]),
                new LegalDocumentSection("4. Registrierung und Nutzerkonto", [
                    "Die Nutzung erfordert eine Registrierung. Der Nutzer muss wahrheitsgemäße, aktuelle Angaben machen und seine Zugangsdaten schützen.",
                    "Der Dienst darf nur von Personen über 18 Jahren bzw. voll geschäftsfähigen Personen genutzt werden.",
                ]),
                new LegalDocumentSection("5. Zahlungen", [
                    "Zahlungen können über Blockchain, PayPal, Stripe oder Revolut erfolgen. Die Zahlungen werden von den jeweiligen Zahlungsdienstleistern nach deren eigenen Bedingungen abgewickelt.",
                    "Alle Zahlungen sind endgültig. Wegen der sofortigen Bereitstellung digitaler Inhalte stimmt der Nutzer dem Beginn der Leistung vor Ablauf der Widerrufsfrist zu und verliert insoweit sein Widerrufsrecht.",
                ]),
                new LegalDocumentSection("6. Pay-to-play-Mechanismus und tokenisiertes Gold", [
                    "Der „Pay-to-play“-Mechanismus ermöglicht es Spielern, tokenisiertes Gold zu erhalten. Tokenisiertes Gold wird über die Tokenisierungsplattform ASA.Gold ausgegeben und verwaltet.",
                    $"Für tokenisiertes Gold gelten die Bedingungen der Tokenisierungsplattform ASA.Gold, abrufbar unter {TokenizationPlatformTermsUrl}.",
                    "Für den Empfang von Zahlungen oder Auszahlungen in tokenisiertem Gold können eine Identitätsprüfung (KYC) und Prüfungen nach den Geldwäschevorschriften (AML) erforderlich sein. Der Anbieter kann Ausweis- und Herkunftsnachweise verlangen und Auszahlungen bis zur Verifizierung zurückhalten oder ablehnen.",
                    "Der Anbieter kann eine Auszahlung ablehnen oder beschränken, wenn der Spieler das KYC/AML-Verfahren nicht abschließt oder ein begründeter Verdacht auf einen Rechts- oder Bedingungsverstoß besteht.",
                ]),
                new LegalDocumentSection("7. Verbotenes Verhalten", [
                    "Der Nutzer verpflichtet sich, keine Fehler, automatisierten Werkzeuge, Betrug oder sonstiges schädigendes Verhalten zu nutzen.",
                    "Bei Verstößen kann der Anbieter das Konto sofort und ohne Entschädigung beschränken oder sperren.",
                ]),
                new LegalDocumentSection("8. Haftungsbeschränkung", [
                    "Soweit gesetzlich zulässig, haftet der Anbieter nicht für indirekte, Folge- oder Zufallsschäden, entgangenen Gewinn, Datenverlust oder Verlust von Spielwert.",
                    "Die Gesamthaftung des Anbieters ist auf den Betrag begrenzt, den der Nutzer in den letzten zwölf Monaten tatsächlich an den Anbieter gezahlt hat.",
                    "Der Anbieter haftet nicht für Ausfälle, technische Fehler, das Handeln Dritter (einschließlich Zahlungsdienstleister und Blockchain-Netzwerke) oder Wertänderungen des tokenisierten Goldes.",
                ]),
                new LegalDocumentSection("9. Änderungen der Bedingungen und des Dienstes", [
                    "Der Anbieter kann diese Bedingungen und den Dienst jederzeit einseitig ändern oder einstellen. Über wesentliche Änderungen wird der Nutzer in geeigneter Weise informiert.",
                    "Durch die fortgesetzte Nutzung nach Inkrafttreten der Änderungen stimmt der Nutzer diesen zu.",
                ]),
                new LegalDocumentSection("10. Beendigung", [
                    "Der Nutzer kann sein Konto jederzeit löschen. Der Anbieter kann den Dienst beenden oder das Konto sperren, insbesondere bei Verstößen gegen diese Bedingungen.",
                    "Bestimmungen, die ihrer Natur nach fortgelten (insbesondere die Haftungsbeschränkung), bleiben wirksam.",
                ]),
                new LegalDocumentSection("11. Schlussbestimmungen", [
                    "Sollte eine Bestimmung dieser Bedingungen unwirksam sein, bleiben die übrigen Bestimmungen wirksam.",
                    $"Fragen zu diesen Bedingungen können an den Anbieter {ProviderName} gerichtet werden.",
                ]),
            ]),
        _ => new LegalDocument(
            LegalDocumentKind.Terms,
            "en",
            "Terms and Conditions",
            Version,
            EffectiveDate,
            $"These Terms and Conditions (the \"Terms\") govern the use of the Capitalism game and platform operated by the service provider {ProviderName}, registration No. {ProviderRegistrationId} (the \"Provider\"). The Provider acts as an ASA.Gold service provider. By registering for or using the service you fully accept these Terms.",
            [
                new LegalDocumentSection("1. Service provider", [
                    $"The operator and provider of the service is {ProviderName}, registration No. {ProviderRegistrationId}, registered in the relevant commercial register of the Slovak Republic.",
                    "The Provider acts as an ASA.Gold service provider and facilitates access to tokenized gold through the ASA.Gold tokenization platform.",
                ]),
                new LegalDocumentSection("2. Governing law and jurisdiction", [
                    "These Terms and all relationships arising from them are governed by the law of the Slovak Republic, in particular Act No. 40/1964 Coll. (Civil Code), Act No. 22/2004 Coll. on electronic commerce, and applicable consumer-protection legislation.",
                    "The courts of the Slovak Republic have jurisdiction over disputes. Consumers may also turn to an alternative dispute resolution body.",
                ]),
                new LegalDocumentSection("3. Scope of the service", [
                    "Capitalism is an online economic game. The Provider supplies access on an \"as is\" and \"as available\" basis, with no warranty of uninterrupted availability.",
                    "In-game currency, virtual items and game progress have no monetary value and are not exchangeable for money, except for tokenized gold under Article 6.",
                ]),
                new LegalDocumentSection("4. Registration and user account", [
                    "Use of the service requires registration. The user must provide truthful, up-to-date information and protect their login credentials.",
                    "The service may only be used by persons over 18 years of age, or persons with full legal capacity.",
                ]),
                new LegalDocumentSection("5. Payments", [
                    "Payments may be made through blockchain, PayPal, Stripe or Revolut. Payments are processed by the respective payment service providers under their own terms.",
                    "All payments are final. Because digital content is delivered immediately, the user consents to performance beginning before the withdrawal period expires and, to that extent, loses the right of withdrawal.",
                ]),
                new LegalDocumentSection("6. Pay-to-play mechanism and tokenized gold", [
                    "The \"pay-to-play\" mechanism allows players to receive tokenized gold. Tokenized gold is issued and administered through the ASA.Gold tokenization platform.",
                    $"Tokenized gold is subject to the terms of the ASA.Gold tokenization platform available at {TokenizationPlatformTermsUrl}, which the user must review.",
                    "To receive payments or payouts in tokenized gold, identity verification (KYC) and anti-money-laundering (AML) checks may be required. The Provider may request identity and source-of-funds documents and may withhold or refuse a payout until verification is complete.",
                    "The Provider may refuse or restrict a payout if the player does not complete the KYC/AML process, or if there is a reasonable suspicion of a breach of law or of these Terms.",
                ]),
                new LegalDocumentSection("7. Prohibited conduct", [
                    "The user undertakes not to exploit bugs, automated tools, fraud or any other conduct harmful to the service or other users.",
                    "In the event of a breach, the Provider may immediately restrict or terminate the account without compensation.",
                ]),
                new LegalDocumentSection("8. Limitation of liability", [
                    "To the maximum extent permitted by law, the Provider is not liable for any indirect, consequential or incidental damage, lost profit, loss of data or loss of game value.",
                    "The Provider's total liability is limited to the amount actually paid by the user to the Provider during the last twelve months.",
                    "The Provider is not liable for outages, technical errors, the acts of third parties (including payment service providers and blockchain networks) or changes in the value of tokenized gold.",
                ]),
                new LegalDocumentSection("9. Changes to the Terms and the service", [
                    "The Provider may unilaterally change or discontinue these Terms and the service at any time. The user will be informed of material changes in an appropriate manner.",
                    "By continuing to use the service after the changes take effect, the user accepts them.",
                ]),
                new LegalDocumentSection("10. Termination", [
                    "The user may delete their account at any time. The Provider may terminate the service or close an account, in particular for a breach of these Terms.",
                    "Provisions that by their nature survive termination (in particular the limitation of liability) remain in force.",
                ]),
                new LegalDocumentSection("11. Final provisions", [
                    "If any provision of these Terms becomes invalid, the remaining provisions remain in force.",
                    $"Questions regarding these Terms may be addressed to the Provider {ProviderName}.",
                ]),
            ]),
    };

    private static LegalDocument Privacy(string locale) => locale switch
    {
        "sk" => new LegalDocument(
            LegalDocumentKind.Privacy,
            "sk",
            "Zásady ochrany osobných údajov",
            Version,
            EffectiveDate,
            $"Tieto Zásady ochrany osobných údajov opisujú, ako poskytovateľ služby {ProviderName}, IČO {ProviderRegistrationId} (ďalej len „Prevádzkovateľ“), spracúva osobné údaje používateľov hry Capitalism v súlade s nariadením (EÚ) 2016/679 (GDPR) a zákonom č. 18/2018 Z. z.",
            [
                new LegalDocumentSection("1. Prevádzkovateľ", [
                    $"Prevádzkovateľom osobných údajov je {ProviderName}, IČO {ProviderRegistrationId}.",
                ]),
                new LegalDocumentSection("2. Aké údaje spracúvame", [
                    "Spracúvame najmä registračné údaje (e-mail, zobrazované meno), údaje o účte a hernom postupe, technické údaje (IP adresa, údaje o zariadení) a v prípade výplat tokenizovaného zlata aj údaje potrebné na overenie totožnosti (KYC/AML).",
                ]),
                new LegalDocumentSection("3. Účely a právne základy", [
                    "Údaje spracúvame na účely poskytovania služby (plnenie zmluvy), zabezpečenia a prevencie podvodov (oprávnený záujem), plnenia zákonných povinností vrátane povinností podľa predpisov AML a na zasielanie servisných e-mailov.",
                ]),
                new LegalDocumentSection("4. Uloženie údajov v EÚ", [
                    "Osobné údaje sú uložené a spracúvané na serveroch nachádzajúcich sa na území Európskej únie (EÚ/EHP).",
                ]),
                new LegalDocumentSection("5. Príjemcovia a sprostredkovatelia", [
                    "Údaje môžu byť poskytnuté poskytovateľom platobných služieb (blockchain, PayPal, Stripe, Revolut), poskytovateľom cloudových a e-mailových služieb a tokenizačnej platforme ASA.Gold, a to v rozsahu nevyhnutnom na poskytnutie služby.",
                ]),
                new LegalDocumentSection("6. Doba uchovávania", [
                    "Osobné údaje uchovávame po dobu trvania účtu a následne po dobu vyžadovanú právnymi predpismi (najmä účtovnými a AML predpismi).",
                ]),
                new LegalDocumentSection("7. Práva dotknutej osoby", [
                    "Máte právo na prístup k údajom, ich opravu, vymazanie, obmedzenie spracúvania, prenosnosť a právo namietať. Máte tiež právo podať sťažnosť Úradu na ochranu osobných údajov Slovenskej republiky.",
                ]),
                new LegalDocumentSection("8. Kontakt", [
                    $"Vo veciach ochrany osobných údajov nás môžete kontaktovať na adrese Prevádzkovateľa {ProviderName}.",
                ]),
            ]),
        "de" => new LegalDocument(
            LegalDocumentKind.Privacy,
            "de",
            "Datenschutzerklärung",
            Version,
            EffectiveDate,
            $"Diese Datenschutzerklärung beschreibt, wie der Dienstanbieter {ProviderName}, IČO {ProviderRegistrationId} (nachfolgend „Verantwortlicher“), personenbezogene Daten der Nutzer des Spiels Capitalism gemäß der Verordnung (EU) 2016/679 (DSGVO) und dem Gesetz Nr. 18/2018 Slg. verarbeitet.",
            [
                new LegalDocumentSection("1. Verantwortlicher", [
                    $"Verantwortlicher für die Datenverarbeitung ist {ProviderName}, IČO {ProviderRegistrationId}.",
                ]),
                new LegalDocumentSection("2. Welche Daten wir verarbeiten", [
                    "Wir verarbeiten insbesondere Registrierungsdaten (E-Mail, Anzeigename), Konto- und Spielfortschrittsdaten, technische Daten (IP-Adresse, Geräteinformationen) sowie – bei Auszahlungen von tokenisiertem Gold – die zur Identitätsprüfung erforderlichen Daten (KYC/AML).",
                ]),
                new LegalDocumentSection("3. Zwecke und Rechtsgrundlagen", [
                    "Wir verarbeiten Daten zur Erbringung des Dienstes (Vertragserfüllung), zur Sicherheit und Betrugsprävention (berechtigtes Interesse), zur Erfüllung gesetzlicher Pflichten einschließlich AML-Vorschriften sowie zum Versand von Service-E-Mails.",
                ]),
                new LegalDocumentSection("4. Datenspeicherung in der EU", [
                    "Personenbezogene Daten werden auf Servern innerhalb der Europäischen Union (EU/EWR) gespeichert und verarbeitet.",
                ]),
                new LegalDocumentSection("5. Empfänger und Auftragsverarbeiter", [
                    "Daten können an Zahlungsdienstleister (Blockchain, PayPal, Stripe, Revolut), Cloud- und E-Mail-Dienstleister sowie an die Tokenisierungsplattform ASA.Gold weitergegeben werden, soweit dies für die Erbringung des Dienstes erforderlich ist.",
                ]),
                new LegalDocumentSection("6. Speicherdauer", [
                    "Wir speichern personenbezogene Daten für die Dauer des Kontos und anschließend für den gesetzlich vorgeschriebenen Zeitraum (insbesondere Buchhaltungs- und AML-Vorschriften).",
                ]),
                new LegalDocumentSection("7. Rechte der betroffenen Person", [
                    "Sie haben das Recht auf Auskunft, Berichtigung, Löschung, Einschränkung der Verarbeitung, Datenübertragbarkeit und Widerspruch. Sie haben außerdem das Recht, eine Beschwerde beim Amt für den Schutz personenbezogener Daten der Slowakischen Republik einzureichen.",
                ]),
                new LegalDocumentSection("8. Kontakt", [
                    $"In Datenschutzangelegenheiten können Sie uns unter der Anschrift des Verantwortlichen {ProviderName} kontaktieren.",
                ]),
            ]),
        _ => new LegalDocument(
            LegalDocumentKind.Privacy,
            "en",
            "Privacy Policy",
            Version,
            EffectiveDate,
            $"This Privacy Policy describes how the service provider {ProviderName}, registration No. {ProviderRegistrationId} (the \"Controller\"), processes the personal data of Capitalism users in accordance with Regulation (EU) 2016/679 (GDPR) and Act No. 18/2018 Coll.",
            [
                new LegalDocumentSection("1. Controller", [
                    $"The controller of personal data is {ProviderName}, registration No. {ProviderRegistrationId}.",
                ]),
                new LegalDocumentSection("2. Data we process", [
                    "We process in particular registration data (email, display name), account and game-progress data, technical data (IP address, device information) and, for tokenized gold payouts, the data required for identity verification (KYC/AML).",
                ]),
                new LegalDocumentSection("3. Purposes and legal bases", [
                    "We process data to provide the service (performance of a contract), for security and fraud prevention (legitimate interest), to comply with legal obligations including AML rules, and to send service emails.",
                ]),
                new LegalDocumentSection("4. Data storage in the EU", [
                    "Personal data is stored and processed on servers located within the European Union (EU/EEA).",
                ]),
                new LegalDocumentSection("5. Recipients and processors", [
                    "Data may be shared with payment service providers (blockchain, PayPal, Stripe, Revolut), cloud and email service providers, and the ASA.Gold tokenization platform, to the extent necessary to provide the service.",
                ]),
                new LegalDocumentSection("6. Retention period", [
                    "We retain personal data for the duration of the account and afterwards for the period required by law (in particular accounting and AML legislation).",
                ]),
                new LegalDocumentSection("7. Rights of the data subject", [
                    "You have the right of access, rectification, erasure, restriction of processing, data portability and objection. You also have the right to lodge a complaint with the Office for Personal Data Protection of the Slovak Republic.",
                ]),
                new LegalDocumentSection("8. Contact", [
                    $"For data protection matters you may contact us at the address of the Controller {ProviderName}.",
                ]),
            ]),
    };
}
