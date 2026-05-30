namespace MasterApi.Utilities;

public static class EmailLocalizations
{
    public static readonly IReadOnlySet<string> SupportedLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "sk",
        "de",
    };

    public static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en";
        }

        var normalized = locale.Trim().ToLowerInvariant();
        var shortLocale = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return shortLocale is not null && SupportedLocales.Contains(shortLocale) ? shortLocale : "en";
    }

    public static string RegistrationLegalNote(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "V prílohe tohto e-mailu nájdete Všeobecné obchodné podmienky a Zásady ochrany osobných údajov vo formáte PDF. Registráciou ste s nimi vyjadrili súhlas.",
        "de" => "Im Anhang dieser E-Mail finden Sie die Allgemeinen Geschäftsbedingungen und die Datenschutzerklärung als PDF. Mit der Registrierung haben Sie ihnen zugestimmt.",
        _ => "Attached to this email you will find the Terms and Conditions and the Privacy Policy as PDF files. By registering you agreed to them.",
    };

    public static EmailCopy Registration(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Vitajte v Capitalism",
            "Vitajte vo svojej obchodnej impérii",
            "Sme radi, že ste sa pridali. Váš účet je pripravený a môžete začať budovať firmu na aktívnych herných serveroch.",
            "Adresa, ktorú ste navštívili pri registrácii:",
            "Tento e-mail ste dostali, pretože ste si vytvorili účet v hre Capitalism."),
        "de" => new EmailCopy(
            "Willkommen bei Capitalism",
            "Willkommen in deinem Wirtschaftsimperium",
            "Schön, dass du dabei bist. Dein Konto ist bereit und du kannst auf den aktiven Spielservern mit dem Aufbau deines Unternehmens beginnen.",
            "Adresse, die du bei der Registrierung geöffnet hast:",
            "Du erhältst diese E-Mail, weil du ein Capitalism-Konto erstellt hast."),
        _ => new EmailCopy(
            "Welcome to Capitalism",
            "Welcome to your business empire",
            "We are glad you joined. Your account is ready and you can start building your company on the active game servers.",
            "URL you accessed when registering:",
            "You received this email because you created a Capitalism account."),
    };

    public static EmailCopy WeeklyReport(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Týždenný report Capitalism",
            "Váš týždenný ekonomický report",
            "Tu je súhrn aktívnych serverov, bounty bodov a changelogu za posledný týždeň.",
            "Aktívne herné servery",
            "Tento týždenný report posielame v piatok napoludnie podľa UTC."),
        "de" => new EmailCopy(
            "Wöchentlicher Capitalism-Bericht",
            "Dein wöchentlicher Wirtschaftsbericht",
            "Hier ist deine Übersicht der aktiven Server, Bounty-Punkte und Changelog-Neuigkeiten der letzten Woche.",
            "Aktive Spielserver",
            "Dieser Wochenbericht wird freitags um 12:00 Uhr UTC gesendet."),
        _ => new EmailCopy(
            "Your weekly Capitalism report",
            "Your weekly economy report",
            "Here is your summary of active servers, bounty points, and changelog news from the past week.",
            "Active game servers",
            "This weekly report is sent at Friday noon UTC."),
    };

    public static string WeeklyNoActiveServers(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Tento týždeň neboli dostupné žiadne aktívne herné servery.",
        "de" => "In dieser Woche waren keine aktiven Spielserver verfügbar.",
        _ => "No active game servers were available this week.",
    };

    public static string WeeklyChangelogFallbackTitle(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Aktualizácia Capitalism",
        "de" => "Capitalism-Aktualisierung",
        _ => "Capitalism update",
    };

    public static string WeeklyProfitLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Zisk",
        "de" => "Gewinn",
        _ => "Profit",
    };

    public static string WeeklyRankLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Poradie",
        "de" => "Rang",
        _ => "Rank",
    };

    public static string WeeklyBountiesLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Bounty",
        "de" => "Bounties",
        _ => "Bounties",
    };

    public static string WeeklyProfitUnavailable(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "nedostupné",
        "de" => "nicht verfügbar",
        _ => "unavailable",
    };

    public static string WeeklyMasterBountyLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Master bounty body tento týždeň",
        "de" => "Master-Bounty-Punkte diese Woche",
        _ => "Master bounty points this week",
    };

    public static EmailCopy AdminTest(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Testovací e-mail Capitalism",
            "Test doručovania e-mailov",
            "Toto je testovací e-mail odoslaný administrátorom, ktorý overuje šablónu a doručovanie.",
            "Správa od administrátora",
            "Tento e-mail ste dostali, pretože administrátor testoval doručovanie e-mailov v hre Capitalism."),
        "de" => new EmailCopy(
            "Capitalism-Test-E-Mail",
            "Test der E-Mail-Zustellung",
            "Dies ist eine Test-E-Mail eines Administrators, um Vorlage und Zustellung zu prüfen.",
            "Nachricht des Administrators",
            "Du erhältst diese E-Mail, weil ein Administrator die E-Mail-Zustellung in Capitalism getestet hat."),
        _ => new EmailCopy(
            "Capitalism test email",
            "Email delivery test",
            "This is a test email sent by an administrator to verify the template and delivery.",
            "Administrator message",
            "You received this email because an administrator tested Capitalism email delivery."),
    };

    public static EmailCopy SupportTicketCreated(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Vaša požiadavka podpory bola prijatá",
            "Požiadavka podpory bola prijatá",
            "Dostali sme vašu požiadavku podpory. Nižšie je text požiadavky, ktorý ste odoslali.",
            "Text požiadavky",
            "Tento e-mail ste dostali, pretože ste vytvorili požiadavku podpory v hre Capitalism."),
        "de" => new EmailCopy(
            "Dein Support-Ticket wurde empfangen",
            "Support-Ticket empfangen",
            "Wir haben dein Support-Ticket erhalten. Unten steht der Tickettext, den du gesendet hast.",
            "Tickettext",
            "Du erhältst diese E-Mail, weil du ein Support-Ticket in Capitalism erstellt hast."),
        _ => new EmailCopy(
            "Your support ticket was received",
            "Support ticket received",
            "We received your support ticket. The ticket text you submitted is included below.",
            "Ticket text",
            "You received this email because you created a Capitalism support ticket."),
    };

    public static EmailCopy SupportTicketUpdated(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Vaša požiadavka podpory bola aktualizovaná",
            "Požiadavka podpory bola aktualizovaná",
            "Vaša požiadavka podpory sa zmenila. Nižšie sú aktuálne údaje požiadavky.",
            "Aktuálny text požiadavky",
            "Tento e-mail ste dostali, pretože sa zmenila vaša požiadavka podpory v hre Capitalism."),
        "de" => new EmailCopy(
            "Dein Support-Ticket wurde aktualisiert",
            "Support-Ticket aktualisiert",
            "Dein Support-Ticket wurde geändert. Unten stehen die aktuellen Ticketdetails.",
            "Aktueller Tickettext",
            "Du erhältst diese E-Mail, weil dein Capitalism-Support-Ticket geändert wurde."),
        _ => new EmailCopy(
            "Your support ticket was updated",
            "Support ticket updated",
            "Your support ticket changed. The current ticket details are included below.",
            "Current ticket text",
            "You received this email because your Capitalism support ticket changed."),
    };

    public static string SupportTicketTypeLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Typ",
        "de" => "Typ",
        _ => "Type",
    };

    public static string SupportTicketStatusLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Stav",
        "de" => "Status",
        _ => "Status",
    };

    public static string SupportTicketTitleLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Názov",
        "de" => "Titel",
        _ => "Title",
    };

    public static string SupportTicketChangeLabel(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Zmena",
        "de" => "Änderung",
        _ => "Change",
    };

    public static string SupportTicketContentUpdatedNote(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => "Text požiadavky podpory bol aktualizovaný.",
        "de" => "Der Support-Tickettext wurde aktualisiert.",
        _ => "Support ticket content was updated.",
    };

    public static string SupportTicketStatusChangedNote(string locale, string status) => NormalizeLocale(locale) switch
    {
        "sk" => $"Stav požiadavky podpory sa zmenil na {status}.",
        "de" => $"Der Support-Ticketstatus wurde zu {status} geändert.",
        _ => $"Support ticket status changed to {status}.",
    };

    public static EmailCopy AccountDeletionRequested(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Žiadosť o vymazanie účtu Capitalism",
            "Váš účet je naplánovaný na vymazanie",
            "Dostali sme žiadosť o vymazanie vášho účtu Capitalism. Mrzí nás, že odchádzate. Pred trvalým odstránením máte ešte čas si to rozmyslieť.",
            "Čo stratíte",
            "Tento e-mail ste dostali, pretože bola odoslaná žiadosť o vymazanie vášho účtu Capitalism."),
        "de" => new EmailCopy(
            "Anfrage zur Löschung deines Capitalism-Kontos",
            "Dein Konto ist zur Löschung vorgemerkt",
            "Wir haben eine Anfrage zur Löschung deines Capitalism-Kontos erhalten. Schade, dass du gehst. Vor der endgültigen Löschung hast du noch Zeit, es dir anders zu überlegen.",
            "Was du verlierst",
            "Du erhältst diese E-Mail, weil eine Anfrage zur Löschung deines Capitalism-Kontos gestellt wurde."),
        _ => new EmailCopy(
            "Capitalism account deletion request",
            "Your account is scheduled for deletion",
            "We received a request to delete your Capitalism account. We are sorry to see you go. Before anything is permanently removed, you still have time to change your mind.",
            "What you will lose",
            "You received this email because a request was made to delete your Capitalism account."),
    };

    public static EmailCopy AccountDeletionCompleted(string locale) => NormalizeLocale(locale) switch
    {
        "sk" => new EmailCopy(
            "Váš účet Capitalism bol vymazaný",
            "Váš účet bol vymazaný",
            "Váš účet Capitalism a súvisiace herné údaje boli odstránené z našich serverov, ako ste požadovali. Ďakujeme, že ste s nami hrali.",
            "Čo nasleduje",
            "Tento e-mail ste dostali, pretože vymazanie vášho účtu Capitalism bolo dokončené."),
        "de" => new EmailCopy(
            "Dein Capitalism-Konto wurde gelöscht",
            "Dein Konto wurde gelöscht",
            "Dein Capitalism-Konto und die zugehörigen Spieldaten wurden wie gewünscht von unseren Servern entfernt. Danke, dass du mit uns gespielt hast.",
            "Wie es weitergeht",
            "Du erhältst diese E-Mail, weil die Löschung deines Capitalism-Kontos abgeschlossen wurde."),
        _ => new EmailCopy(
            "Your Capitalism account has been deleted",
            "Your account has been deleted",
            "Your Capitalism account and the related game data have been removed from our servers as you requested. Thank you for playing with us.",
            "What happens next",
            "You received this email because the deletion of your Capitalism account has been completed."),
    };

    /// <summary>Bullet points describing what the player loses when the account is deleted.</summary>
    public static IReadOnlyList<string> AccountDeletionLossItems(string locale) => NormalizeLocale(locale) switch
    {
        "sk" =>
        [
            "Všetok herný postup na všetkých herných serveroch.",
            "Tokenizované zlaté vklady spojené s vaším účtom.",
            "Akékoľvek budúce tokenizované zlaté odmeny.",
            "Po dokončení budú všetky vaše údaje z herného servera natrvalo odstránené.",
        ],
        "de" =>
        [
            "Deinen gesamten Spielfortschritt auf allen Spielservern.",
            "Die mit deinem Konto verknüpften tokenisierten Goldeinlagen.",
            "Alle zukünftigen tokenisierten Goldbelohnungen.",
            "Nach Abschluss werden alle deine Daten dauerhaft vom Spielserver entfernt.",
        ],
        _ =>
        [
            "All of your game progress across every game server.",
            "The tokenized gold deposits linked to your account.",
            "Any future tokenized gold rewards.",
            "Once completed, all of your data will be permanently removed from the game server.",
        ],
    };

    public static string AccountDeletionScheduledNote(string locale, string scheduledAtUtc) => NormalizeLocale(locale) switch
    {
        "sk" => $"Váš účet bude trvalo odstránený po {scheduledAtUtc}.",
        "de" => $"Dein Konto wird nach {scheduledAtUtc} dauerhaft entfernt.",
        _ => $"Your account will be permanently removed after {scheduledAtUtc}.",
    };

    public static string AccountDeletionCancelNote(string locale, string portalUrl) => NormalizeLocale(locale) switch
    {
        "sk" => $"Rozmysleli ste si to? Vymazanie môžete kedykoľvek počas tejto lehoty zrušiť v nastaveniach účtu: {portalUrl}",
        "de" => $"Anders überlegt? Du kannst die Löschung während dieser Frist jederzeit in deinen Kontoeinstellungen abbrechen: {portalUrl}",
        _ => $"Changed your mind? You can cancel the deletion at any time during this period from your account settings: {portalUrl}",
    };

    public static string AccountDeletionPortalNote(string locale, string portalUrl) => NormalizeLocale(locale) switch
    {
        "sk" => $"Ak budete chcieť, ste vždy vítaní späť. Nový účet si môžete vytvoriť na {portalUrl}",
        "de" => $"Du bist jederzeit wieder willkommen. Ein neues Konto kannst du unter {portalUrl} erstellen.",
        _ => $"You are always welcome back. You can create a new account any time at {portalUrl}",
    };
}

public sealed record EmailCopy(
    string Subject,
    string Headline,
    string Intro,
    string SectionTitle,
    string Footer);
