using ClaudeBuddy.Core;

namespace ClaudeBuddy.Content;

/// <summary>
/// Fixed UI strings (menu items, dialogs, combo cheers, short reactions) in every supported
/// language. These are the NON-random texts — the random chatter lives in <see cref="Phrasebook"/>.
///
/// Each entry maps a stable key to one line per language, in <see cref="Language"/> order
/// (English, Spanish, Portuguese, French, German, Italian). Translations keep the original's
/// playful, warm tone rather than being literal. <see cref="T"/> returns the active language's
/// line, falling back to English (then Spanish) if a cell is ever left null.
/// </summary>
public sealed class Strings
{
    private readonly Localization _loc;

    public Strings(Localization loc) => _loc = loc;

    /// <summary>Returns the localized string for <paramref name="key"/> (English fallback).</summary>
    public string T(string key)
    {
        if (!Table.TryGetValue(key, out string?[]? row))
        {
            return key; // unknown key: surface it rather than crash
        }

        return Pick(row, _loc.Current);
    }

    /// <summary>Like <see cref="T"/> but with a {0} placeholder filled in (combo counts, etc.).</summary>
    public string T(string key, object arg0) => string.Format(Pick(Lookup(key), _loc.Current), arg0);

    private static string?[] Lookup(string key) =>
        Table.TryGetValue(key, out string?[]? row) ? row : new string?[] { key };

    // Order MUST match the Language enum (skip Auto, which is index 0 and never stored).
    private static int Index(Language lang) => lang switch
    {
        Language.English => 0,
        Language.Spanish => 1,
        Language.Portuguese => 2,
        Language.French => 3,
        Language.German => 4,
        Language.Italian => 5,
        _ => 0,
    };

    private static string Pick(string?[] row, Language lang)
    {
        int i = Index(lang);
        if (i < row.Length && row[i] is { } s)
        {
            return s;
        }

        // Fallbacks: English (0), then Spanish (1), then the first non-null cell.
        if (row.Length > 0 && row[0] is { } en) { return en; }
        if (row.Length > 1 && row[1] is { } es) { return es; }
        foreach (string? cell in row)
        {
            if (cell is { } any) { return any; }
        }

        return string.Empty;
    }

    // Helper to keep the table readable: en, es, pt, fr, de, it.
    private static string?[] L(string en, string es, string pt, string fr, string de, string it) =>
        new string?[] { en, es, pt, fr, de, it };

    private static readonly Dictionary<string, string?[]> Table = new(StringComparer.Ordinal)
    {
        // ---- Right-click menu ------------------------------------------------
        ["menu.open_claude"] = L("Open Claude", "Abrir Claude", "Abrir Claude", "Ouvrir Claude", "Claude öffnen", "Apri Claude"),
        ["menu.change_skin"] = L("Change Skin", "Cambiar Skin", "Mudar Skin", "Changer de skin", "Skin ändern", "Cambia skin"),
        ["menu.animation_speed"] = L("Animation Speed", "Velocidad de animación", "Velocidade da animação", "Vitesse d'animation", "Animationstempo", "Velocità animazione"),
        ["menu.behaviour_frequency"] = L("Behaviour Frequency", "Frecuencia de comportamiento", "Frequência de comportamento", "Fréquence des comportements", "Verhaltenshäufigkeit", "Frequenza comportamenti"),
        ["menu.play_animation"] = L("Play Animation", "Reproducir animación", "Reproduzir animação", "Jouer une animation", "Animation abspielen", "Riproduci animazione"),
        ["menu.language"] = L("Language", "Idioma", "Idioma", "Langue", "Sprache", "Lingua"),
        ["menu.language_auto"] = L("Auto (system)", "Automático (sistema)", "Automático (sistema)", "Auto (système)", "Automatisch (System)", "Automatico (sistema)"),
        ["menu.always_on_top"] = L("Always On Top", "Siempre visible", "Sempre no topo", "Toujours au-dessus", "Immer im Vordergrund", "Sempre in primo piano"),
        ["menu.launch_on_startup"] = L("Launch On Startup", "Iniciar con Windows", "Iniciar com o sistema", "Lancer au démarrage", "Mit Windows starten", "Avvia all'avvio"),
        ["menu.photo_mode"] = L("Photo Mode", "Modo foto", "Modo foto", "Mode photo", "Fotomodus", "Modalità foto"),
        ["menu.show_battery"] = L("Show Session Battery", "Mostrar batería de sesión", "Mostrar bateria da sessão", "Afficher la batterie de session", "Sitzungsakku anzeigen", "Mostra batteria sessione"),
        ["menu.world_data"] = L("World Data (weather, etc.)", "Datos del mundo (clima, etc.)", "Dados do mundo (clima, etc.)", "Données du monde (météo, etc.)", "Weltdaten (Wetter usw.)", "Dati dal mondo (meteo, ecc.)"),
        ["menu.reset_position"] = L("Reset Position", "Reiniciar posición", "Redefinir posição", "Réinitialiser la position", "Position zurücksetzen", "Reimposta posizione"),
        ["menu.achievements"] = L("Achievements…", "Logros…", "Conquistas…", "Succès…", "Erfolge…", "Obiettivi…"),
        ["menu.mods"] = L("Mods…", "Mods…", "Mods…", "Mods…", "Mods…", "Mods…"),
        ["menu.settings_folder"] = L("Settings Folder…", "Carpeta de ajustes…", "Pasta de configurações…", "Dossier des réglages…", "Einstellungsordner…", "Cartella impostazioni…"),
        ["menu.about"] = L("About Claude Buddy", "Acerca de Claude Buddy", "Sobre o Claude Buddy", "À propos de Claude Buddy", "Über Claude Buddy", "Informazioni su Claude Buddy"),
        ["menu.exit"] = L("Exit", "Salir", "Sair", "Quitter", "Beenden", "Esci"),

        // Behaviour-frequency preset names.
        ["freq.calm"] = L("Calm", "Tranquilo", "Calmo", "Calme", "Ruhig", "Calmo"),
        ["freq.normal"] = L("Normal", "Normal", "Normal", "Normal", "Normal", "Normale"),
        ["freq.lively"] = L("Lively", "Animado", "Animado", "Vif", "Lebhaft", "Vivace"),
        ["freq.hyper"] = L("Hyper", "Híper", "Híper", "Hyper", "Hyper", "Iper"),

        // ---- Keep-up juggling mini-game --------------------------------------
        ["combo.record"] = L("Record! x{0}", "¡Récord! x{0}", "Recorde! x{0}", "Record ! x{0}", "Rekord! x{0}", "Record! x{0}"),
        ["combo.broke"] = L("Aww! You dropped it at x{0}", "¡Uf! Cortaste en x{0}", "Eita! Você parou em x{0}", "Aïe ! Tu t'es arrêté à x{0}", "Och! Bei x{0} abgebrochen", "Ahi! Ti sei fermato a x{0}"),

        // Combo cheers (one is picked at random; see Phrasebook for the pool driver).
        ["combo.cheer.0"] = L("Combo!", "¡Combo!", "Combo!", "Combo !", "Combo!", "Combo!"),
        ["combo.cheer.1"] = L("Keep it up!", "¡Seguí así!", "Continua assim!", "Continue !", "Weiter so!", "Continua così!"),
        ["combo.cheer.2"] = L("Unstoppable!", "¡Imparable!", "Imparável!", "Imparable !", "Unaufhaltsam!", "Inarrestabile!"),
        ["combo.cheer.3"] = L("You're a star!", "¡Sos un crack!", "Você é fera!", "T'es un crack !", "Du bist ein Star!", "Sei un fenomeno!"),
        ["combo.cheer.4"] = L("Don't drop me!", "¡No me sueltes!", "Não me solta!", "Me lâche pas !", "Lass mich nicht fallen!", "Non mollarmi!"),

        // ---- Rough-handling ("abuse") reactions ------------------------------
        ["abuse.0"] = L("...", "...", "...", "...", "...", "..."),
        ["abuse.1"] = L("Again?", "¿Otra vez?", "De novo?", "Encore ?", "Schon wieder?", "Ancora?"),
        ["abuse.2"] = L("Seriously?", "¿En serio?", "Sério?", "Sérieux ?", "Im Ernst?", "Sul serio?"),
        ["abuse.3"] = L("Okay, enough...", "Ya basta...", "Já chega...", "Ça suffit...", "Jetzt reicht's...", "Adesso basta..."),
        ["abuse.4"] = L("I'm getting dizzy...", "Me mareo...", "Tô tonto...", "J'ai le vertige...", "Mir wird schwindelig...", "Mi gira la testa..."),
        ["abuse.5"] = L("Uff, stop", "Uff, basta", "Ufa, para", "Ouf, arrête", "Uff, hör auf", "Uff, basta"),
        ["abuse.6"] = L("Easy now", "Pará un poco", "Vai com calma", "Doucement", "Immer langsam", "Vacci piano"),

        // ---- Clone / portal event -------------------------------------------
        ["clone.is_that_me"] = L("Wait... is that me?!", "¿Y ese... soy yo?!", "Peraí... sou eu?!", "Attends... c'est moi ?!", "Moment... bin das ich?!", "Aspetta... sono io?!"),
        ["clone.a_portal"] = L("A portal opened!", "¡Se abrió un portal!", "Abriu um portal!", "Un portail s'est ouvert !", "Ein Portal hat sich geöffnet!", "Si è aperto un portale!"),

        // ---- Time-of-day greetings (used by the world-data driver) -----------
        ["greet.morning"] = L("Good morning!", "¡Buenos días!", "Bom dia!", "Bonjour !", "Guten Morgen!", "Buongiorno!"),
        ["greet.afternoon"] = L("Good afternoon!", "¡Buenas tardes!", "Boa tarde!", "Bon après-midi !", "Guten Tag!", "Buon pomeriggio!"),
        ["greet.evening"] = L("Good evening!", "¡Buenas noches!", "Boa noite!", "Bonsoir !", "Guten Abend!", "Buonasera!"),
    };
}
