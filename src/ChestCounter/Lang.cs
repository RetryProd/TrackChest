using System.Globalization;

namespace ChestCounter;

/// <summary>
/// User interface language: French, English, Spanish, German, Russian or Japanese. Saved in config.json
/// (<see cref="Config.Language"/>); when it is empty the language of Windows is used (English when it is not one of ours).
/// Texts are written inline as <c>L.T("français", "English", "español", "Deutsch", "русский", "日本語")</c>. Names stored
/// in data.json (chest kinds, treasure items) are kept in French for compatibility and translated when displayed (<see cref="Name"/>).
/// </summary>
public static class L
{
    /// <summary>Languages offered, in menu order.</summary>
    public static readonly string[] Codes = { "fr", "en", "es", "de", "ru", "ja" };

    /// <summary>"fr", "en", "es", "de", "ru" or "ja".</summary>
    public static string Code { get; private set; } = "fr";

    /// <summary>True when the language follows Windows (nothing chosen in the settings).</summary>
    public static bool Auto { get; private set; } = true;

    public static bool En => Code == "en";
    public static bool Es => Code == "es";

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Applies a saved choice: a language code, or null / "auto" for the language of Windows.</summary>
    public static void Set(string? code)
    {
        var chosen = Normalize(code);
        Auto = chosen == null;
        Code = chosen ?? SystemDefault;
        Culture = CultureInfo.GetCultureInfo(T("fr-FR", "en-GB", "es-ES", "de-DE", "ru-RU", "ja-JP"));
    }

    /// <summary>Language code, or null when the answer is not recognized (or "auto").</summary>
    public static string? Normalize(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "fr" or "fra" or "fre" or "french" or "français" or "francais" or "francés" or "frances" => "fr",
        "en" or "eng" or "english" or "anglais" or "inglés" or "ingles" => "en",
        "es" or "spa" or "spanish" or "español" or "espanol" or "espagnol" => "es",
        "de" or "deu" or "ger" or "german" or "deutsch" or "allemand" => "de",
        "ru" or "rus" or "russian" or "русский" or "russe" => "ru",
        "ja" or "jpn" or "jp" or "japanese" or "日本語" or "japonais" => "ja",
        _ => null,
    };

    /// <summary>Name of a language in that language (menus).</summary>
    public static string NativeName(string code) => code switch
    {
        "en" => "English", "es" => "Español", "de" => "Deutsch", "ru" => "Русский", "ja" => "日本語", _ => "Français",
    };

    /// <summary>Language of Windows when it is one of ours, English otherwise.</summary>
    public static string SystemDefault => Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? "en";

    public static string T(string fr, string en, string es, string de, string ru, string ja) => Code switch
    {
        "en" => en, "es" => es, "de" => de, "ru" => ru, "ja" => ja, _ => fr,
    };

    public static string N(int n) => n.ToString("N0", Culture);

    /// <summary>
    /// "3 coffres" / "3 chests" / … French: singular for 0 and 1; English, Spanish, German: singular for 1 only;
    /// Russian: one / few (2-4) / many; Japanese: number then counter.
    /// </summary>
    public static string Count(int n, string frOne, string frMany, string enOne, string enMany, string esOne, string esMany,
        string deOne, string deMany, string ruOne, string ruFew, string ruMany, string jaCounter) => Code switch
    {
        "en" => $"{N(n)} {(n == 1 ? enOne : enMany)}",
        "es" => $"{N(n)} {(n == 1 ? esOne : esMany)}",
        "de" => $"{N(n)} {(n == 1 ? deOne : deMany)}",
        "ru" => $"{N(n)} {RussianPlural(n, ruOne, ruFew, ruMany)}",
        "ja" => $"{N(n)} {jaCounter}",
        _ => $"{N(n)} {(n > 1 ? frMany : frOne)}",
    };

    private static string RussianPlural(int n, string one, string few, string many)
    {
        var tens = Math.Abs(n) % 100;
        var units = tens % 10;
        if (tens is >= 11 and <= 14) return many;
        return units == 1 ? one : units is >= 2 and <= 4 ? few : many;
    }

    /// <summary>"3 coffres" / "3 chests" / "3 Truhen" / "3 сундука" / "3 個"…</summary>
    public static string Chests(int n) => Count(n, "coffre", "coffres", "chest", "chests", "cofre", "cofres",
        "Truhe", "Truhen", "сундук", "сундука", "сундуков", "個");

    /// <summary>"3 parties" / "3 matches" / …</summary>
    public static string Matches(int n) => Count(n, "partie", "parties", "match", "matches", "partida", "partidas",
        "Match", "Matches", "матч", "матча", "матчей", "試合");

    /// <summary>"3 fichiers" / "3 files" / …</summary>
    public static string Files(int n) => Count(n, "fichier", "fichiers", "file", "files", "archivo", "archivos",
        "Datei", "Dateien", "файл", "файла", "файлов", "件");

    /// <summary>"3 trésors obtenus" / "3 treasures" / …</summary>
    public static string TreasuresObtained(int n) => Count(n, "trésor obtenu", "trésors obtenus", "treasure", "treasures",
        "tesoro obtenido", "tesoros obtenidos", "Schatz erhalten", "Schätze erhalten", "сокровище получено", "сокровища получено",
        "сокровищ получено", "個の宝を入手");

    /// <summary>"12e" / "12th" / "12.º" / "12." / "12-е" / "12位".</summary>
    public static string Ordinal(int n)
    {
        switch (Code)
        {
            case "es": return $"{n}.º";
            case "de": return $"{n}.";
            case "ru": return $"{n}-е";
            case "ja": return $"{n}位";
            case "fr": return n == 1 ? "1er" : $"{n}e";
        }
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Short date + time of a match ("05/11 22:54" / "5 Nov 22:54" / "05.11. 22:54" / "11/05 22:54").</summary>
    public static string DateTimeShort(DateTime d) =>
        d.ToString(T("dd/MM HH:mm", "d MMM HH:mm", "dd/MM HH:mm", "dd.MM. HH:mm", "dd.MM HH:mm", "MM/dd HH:mm"), Culture);

    // French names stored in data.json -> display names in the other languages.
    private static readonly Dictionary<string, (string En, string Es, string De, string Ru, string Ja)> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = ("Chest", "Cofre", "Truhe", "Сундук", "宝箱"),
        ["Coffre rare"] = ("Rare chest", "Cofre raro", "Seltene Truhe", "Редкий сундук", "レア宝箱"),
        ["Coffre enterré"] = ("Buried chest", "Cofre enterrado", "Vergrabene Truhe", "Зарытый сундук", "埋められた宝箱"),
        ["Coffre d'extraction / reliques"] = ("Extraction / relic chest", "Cofre de extracción / reliquias", "Extraktions-/Reliktruhe", "Сундук извлечения / реликвий", "抽出 / レリック宝箱"),
        ["Coffre des affiches d'esprit"] = ("Sprite poster chest", "Cofre de los carteles de espíritu", "Geisterplakat-Truhe", "Сундук с плакатами духов", "スプライトポスターの宝箱"),
        ["Autre coffre"] = ("Other chest", "Otro cofre", "Andere Truhe", "Другой сундук", "その他の宝箱"),
        ["Canon à butin"] = ("Loot cannon", "Cañón de botín", "Beutekanone", "Пушка с добычей", "戦利品キャノン"),
        ["Caisse de munitions"] = ("Ammo box", "Caja de munición", "Munitionskiste", "Ящик с боеприпасами", "弾薬箱"),
        ["Caisse de nourriture"] = ("Food box", "Caja de comida", "Lebensmittelkiste", "Ящик с едой", "食料箱"),
        ["Autre conteneur"] = ("Other container", "Otro contenedor", "Anderer Behälter", "Другой контейнер", "その他のコンテナ"),
        ["Trésor"] = ("Treasure", "Tesoro", "Schatz", "Сокровище", "宝"),
        ["Trésor (type inconnu)"] = ("Treasure gizmo", "Artilugio de tesoro", "Schatz-Gizmo", "Устройство сокровища", "宝のギズモ"),
        ["Gizmo de trésor"] = ("Treasure gizmo", "Artilugio de tesoro", "Schatz-Gizmo", "Устройство сокровища", "宝のギズモ"),
        ["Feu d'artifice doré"] = ("Golden firework", "Fuego artificial dorado", "Goldenes Feuerwerk", "Золотой фейерверк", "ゴールデン花火"),
        ["Extracteur portable"] = ("Portable extractor", "Extractor portátil", "Tragbarer Extraktor", "Переносной экстрактор", "ポータブル抽出機"),
        ["Localisateur d'esprit"] = ("Sprite locator", "Localizador de espíritus", "Geisterorter", "Локатор духов", "スプライトロケーター"),
        ["Localisateur chanceux"] = ("Lucky locator", "Localizador de la suerte", "Glücksorter", "Счастливый локатор", "ラッキーロケーター"),
        ["Accélérateur d'extraction"] = ("Extraction accelerator", "Acelerador de extracción", "Extraktionsbeschleuniger", "Ускоритель извлечения", "抽出アクセラレーター"),
        ["inconnu"] = ("unknown", "desconocido", "unbekannt", "неизвестно", "不明"),
    };

    /// <summary>Display name of a chest kind or treasure item stored in French.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || Code == "fr" || !Names.TryGetValue(stored, out var t))
        {
            return stored ?? "";
        }
        return T(stored, t.En, t.Es, t.De, t.Ru, t.Ja);
    }
}
