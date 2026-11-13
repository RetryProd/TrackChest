using System.Globalization;

namespace ChestCounter;

/// <summary>
/// User interface language: French, English, Spanish, German, Russian, Japanese, Italian, Portuguese (Brazil), Polish,
/// Dutch, Turkish, Korean, Chinese (simplified), Ukrainian, Swedish or Czech. Saved in config.json
/// (<see cref="Config.Language"/>); when it is empty the language of Windows is used (English when it is not one of ours).
/// Texts are written inline as <c>L.T("français", "English", "español", "Deutsch", "русский", "日本語", "italiano",
/// "português", "polski", "Nederlands", "Türkçe", "한국어", "中文", "українська", "svenska", "čeština")</c>. Names stored
/// in data.json (chest kinds, treasure items) are kept in French for compatibility and translated when displayed (<see cref="Name"/>).
/// The 20 languages of <see cref="ExtraCodes"/> are not written inline: their texts come from <c>Lang\&lt;code&gt;.json</c>,
/// looked up by the English text (<see cref="LangExtra"/>).
/// </summary>
public static class L
{
    /// <summary>Languages whose texts are written inline in <see cref="T"/>.</summary>
    private static readonly string[] InlineCodes = { "fr", "en", "es", "de", "ru", "ja", "it", "pt", "pl", "nl", "tr", "ko", "zh", "uk", "sv", "cs" };

    /// <summary>Languages translated in Lang\&lt;code&gt;.json, with their culture and their name in that language.</summary>
    private static readonly (string Code, string Culture, string Name)[] Extra =
    {
        ("zh-tw", "zh-TW", "繁體中文"), ("id", "id-ID", "Bahasa Indonesia"), ("vi", "vi-VN", "Tiếng Việt"), ("th", "th-TH", "ไทย"),
        ("hi", "hi-IN", "हिन्दी"), ("ms", "ms-MY", "Bahasa Melayu"), ("da", "da-DK", "Dansk"), ("nb", "nb-NO", "Norsk"),
        ("fi", "fi-FI", "Suomi"), ("el", "el-GR", "Ελληνικά"), ("hu", "hu-HU", "Magyar"), ("ro", "ro-RO", "Română"),
        ("bg", "bg-BG", "Български"), ("hr", "hr-HR", "Hrvatski"), ("sr", "sr-Latn-RS", "Srpski"), ("sk", "sk-SK", "Slovenčina"),
        ("sl", "sl-SI", "Slovenščina"), ("lt", "lt-LT", "Lietuvių"), ("lv", "lv-LV", "Latviešu"), ("et", "et-EE", "Eesti"),
    };

    /// <summary>Codes of the languages translated in Lang\&lt;code&gt;.json.</summary>
    public static readonly string[] ExtraCodes = Extra.Select(e => e.Code).ToArray();

    /// <summary>Languages offered, in menu order.</summary>
    public static readonly string[] Codes = InlineCodes.Concat(ExtraCodes).ToArray();

    /// <summary>One of <see cref="Codes"/>.</summary>
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
        LangExtra.Load(Code);
        var extra = Array.FindIndex(Extra, e => e.Code == Code);
        Culture = CultureInfo.GetCultureInfo(extra >= 0 ? Extra[extra].Culture : T("fr-FR", "en-GB", "es-ES", "de-DE", "ru-RU", "ja-JP",
            "it-IT", "pt-BR", "pl-PL", "nl-NL", "tr-TR", "ko-KR", "zh-CN", "uk-UA", "sv-SE", "cs-CZ"));
    }

    /// <summary>True for the languages translated in Lang\&lt;code&gt;.json.</summary>
    private static bool IsExtra => Array.IndexOf(ExtraCodes, Code) >= 0;

    /// <summary>Language code, or null when the answer is not recognized (or "auto").</summary>
    public static string? Normalize(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "fr" or "fra" or "fre" or "french" or "français" or "francais" or "francés" or "frances" => "fr",
        "en" or "eng" or "english" or "anglais" or "inglés" or "ingles" => "en",
        "es" or "spa" or "spanish" or "español" or "espanol" or "espagnol" => "es",
        "de" or "deu" or "ger" or "german" or "deutsch" or "allemand" => "de",
        "ru" or "rus" or "russian" or "русский" or "russe" => "ru",
        "ja" or "jpn" or "jp" or "japanese" or "日本語" or "japonais" => "ja",
        "it" or "ita" or "italian" or "italiano" or "italien" => "it",
        "pt" or "por" or "pt-br" or "portuguese" or "português" or "portugues" or "portugais" => "pt",
        "pl" or "pol" or "polish" or "polski" or "polonais" => "pl",
        "nl" or "nld" or "dut" or "dutch" or "nederlands" or "néerlandais" or "neerlandais" => "nl",
        "tr" or "tur" or "turkish" or "türkçe" or "turkce" or "turc" => "tr",
        "ko" or "kor" or "korean" or "한국어" or "coréen" or "coreen" => "ko",
        "zh" or "zho" or "chi" or "zh-cn" or "zh-hans" or "chinese" or "中文" or "简体中文" or "chinois" => "zh",
        "uk" or "ukr" or "ukrainian" or "українська" or "ukrainien" => "uk",
        "sv" or "swe" or "swedish" or "svenska" or "suédois" or "suedois" => "sv",
        "cs" or "ces" or "cze" or "czech" or "čeština" or "cestina" or "tchèque" or "tcheque" => "cs",
        "zh-tw" or "zh-hk" or "zh-mo" or "zh-hant" or "繁體中文" => "zh-tw",
        "no" or "nb" or "nn" or "nob" or "nor" or "norsk" => "nb",
        "sr" or "sr-latn" or "sr-cyrl" or "srp" or "srpski" => "sr",
        { } other => Array.Find(Extra, e => e.Code == other || e.Culture.Equals(other, StringComparison.OrdinalIgnoreCase)
            || e.Name.Equals(other, StringComparison.OrdinalIgnoreCase)).Code,
        _ => null,
    };

    /// <summary>Name of a language in that language (menus).</summary>
    public static string NativeName(string code) => code switch
    {
        "en" => "English", "es" => "Español", "de" => "Deutsch", "ru" => "Русский", "ja" => "日本語",
        "it" => "Italiano", "pt" => "Português (Brasil)", "pl" => "Polski", "nl" => "Nederlands", "tr" => "Türkçe",
        "ko" => "한국어", "zh" => "简体中文", "uk" => "Українська", "sv" => "Svenska", "cs" => "Čeština",
        _ => Array.Find(Extra, e => e.Code == code).Name ?? "Français",
    };

    /// <summary>Language of Windows when it is one of ours, English otherwise.</summary>
    public static string SystemDefault
    {
        get
        {
            var ui = CultureInfo.CurrentUICulture;
            // Traditional Chinese (Taiwan, Hong Kong, Macao) shares the two-letter code "zh" with simplified Chinese.
            if (ui.Name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)
                && (ui.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase) || ui.Name.EndsWith("TW", StringComparison.OrdinalIgnoreCase)
                    || ui.Name.EndsWith("HK", StringComparison.OrdinalIgnoreCase) || ui.Name.EndsWith("MO", StringComparison.OrdinalIgnoreCase)))
            {
                return "zh-tw";
            }
            return Normalize(ui.TwoLetterISOLanguageName) ?? "en";
        }
    }

    public static string T(string fr, string en, string es, string de, string ru, string ja, string it, string pt, string pl,
        string nl, string tr, string ko, string zh, string uk, string sv, string cs) => Code switch
    {
        "en" => en, "es" => es, "de" => de, "ru" => ru, "ja" => ja, "it" => it, "pt" => pt, "pl" => pl, "nl" => nl,
        "tr" => tr, "ko" => ko, "zh" => zh, "uk" => uk, "sv" => sv, "cs" => cs, "fr" => fr, _ => LangExtra.T(en),
    };

    public static string N(int n) => n.ToString("N0", Culture);

    /// <summary>
    /// "3 coffres" / "3 chests" / … French and Portuguese: singular for 0 and 1; English, Spanish, German, Italian,
    /// Dutch, Swedish: singular for 1 only; Russian, Ukrainian, Polish, Czech: one / few (2-4) / many; Turkish: always
    /// singular after a number; Japanese, Korean, Chinese: number then counter.
    /// </summary>
    private readonly record struct Forms(string FrOne, string FrMany, string EnOne, string EnMany, string EsOne, string EsMany,
        string DeOne, string DeMany, string RuOne, string RuFew, string RuMany, string JaCounter,
        string ItOne, string ItMany, string PtOne, string PtMany, string PlOne, string PlFew, string PlMany,
        string NlOne, string NlMany, string Tr, string KoCounter, string ZhCounter,
        string UkOne, string UkFew, string UkMany, string SvOne, string SvMany, string CsOne, string CsFew, string CsMany);

    private static string Count(int n, string kind, Forms f) => $"{N(n)} {Code switch
    {
        _ when IsExtra => LangExtra.Get($"#{kind}.{ExtraPlural(n)}") ?? LangExtra.Get($"#{kind}.other") ?? (n == 1 ? f.EnOne : f.EnMany),
        "en" => n == 1 ? f.EnOne : f.EnMany,
        "es" => n == 1 ? f.EsOne : f.EsMany,
        "de" => n == 1 ? f.DeOne : f.DeMany,
        "ru" => EastSlavicPlural(n, f.RuOne, f.RuFew, f.RuMany),
        "ja" => f.JaCounter,
        "it" => n == 1 ? f.ItOne : f.ItMany,
        "pt" => n is 0 or 1 ? f.PtOne : f.PtMany,
        "pl" => PolishPlural(n, f.PlOne, f.PlFew, f.PlMany),
        "nl" => n == 1 ? f.NlOne : f.NlMany,
        "tr" => f.Tr,
        "ko" => f.KoCounter,
        "zh" => f.ZhCounter,
        "uk" => EastSlavicPlural(n, f.UkOne, f.UkFew, f.UkMany),
        "sv" => n == 1 ? f.SvOne : f.SvMany,
        "cs" => n == 1 ? f.CsOne : n is >= 2 and <= 4 ? f.CsFew : f.CsMany,
        _ => n > 1 ? f.FrMany : f.FrOne,
    }}";

    /// <summary>Russian and Ukrainian: 1, 21, 31… one; 2-4, 22-24… few; the rest (and 11-14) many.</summary>
    private static string EastSlavicPlural(int n, string one, string few, string many)
    {
        var tens = Math.Abs(n) % 100;
        var units = tens % 10;
        if (tens is >= 11 and <= 14) return many;
        return units == 1 ? one : units is >= 2 and <= 4 ? few : many;
    }

    /// <summary>
    /// Plural category of an added language, used as the suffix of its #kind.* keys (one, two, few, zero, other).
    /// Chinese, Indonesian, Vietnamese, Thai, Malay: no plural. Hindi: one for 0 and 1. Romanian: one / few (0, 2-19,
    /// x01-x19) / other ("de"). Croatian, Serbian: like Russian. Slovak: like Czech. Slovenian: by the last two
    /// digits (1, 2, 3-4). Lithuanian, Latvian: by the last digit, except 11-19.
    /// </summary>
    private static string ExtraPlural(int n)
    {
        var tens = Math.Abs(n) % 100;
        var units = tens % 10;
        return Code switch
        {
            "zh-tw" or "id" or "vi" or "th" or "ms" => "other",
            "hi" => n is 0 or 1 ? "one" : "other",
            "ro" => n == 1 ? "one" : n == 0 || tens is >= 1 and <= 19 ? "few" : "other",
            "hr" or "sr" => tens is >= 11 and <= 14 ? "other" : units == 1 ? "one" : units is >= 2 and <= 4 ? "few" : "other",
            "sk" => n == 1 ? "one" : n is >= 2 and <= 4 ? "few" : "other",
            "sl" => tens == 1 ? "one" : tens == 2 ? "two" : tens is 3 or 4 ? "few" : "other",
            "lt" => tens is >= 11 and <= 19 ? "other" : units == 1 ? "one" : units >= 2 ? "few" : "other",
            "lv" => units == 0 || tens is >= 11 and <= 19 ? "zero" : units == 1 ? "one" : "other",
            _ => n == 1 ? "one" : "other",
        };
    }

    /// <summary>Polish: 1 one; 2-4, 22-24… few (not 12-14); the rest many.</summary>
    private static string PolishPlural(int n, string one, string few, string many)
    {
        if (n == 1) return one;
        var tens = Math.Abs(n) % 100;
        var units = tens % 10;
        return units is >= 2 and <= 4 && tens is not (>= 12 and <= 14) ? few : many;
    }

    /// <summary>"3 coffres" / "3 chests" / "3 Truhen" / "3 сундука" / "3 個"…</summary>
    public static string Chests(int n) => Count(n, "chest", new("coffre", "coffres", "chest", "chests", "cofre", "cofres",
        "Truhe", "Truhen", "сундук", "сундука", "сундуков", "個",
        "forziere", "forzieri", "baú", "baús", "skrzynia", "skrzynie", "skrzyń",
        "kist", "kisten", "sandık", "개", "个宝箱",
        "скриня", "скрині", "скринь", "kista", "kistor", "truhla", "truhly", "truhel"));

    /// <summary>"3 parties" / "3 matches" / …</summary>
    public static string Matches(int n) => Count(n, "match", new("partie", "parties", "match", "matches", "partida", "partidas",
        "Match", "Matches", "матч", "матча", "матчей", "試合",
        "partita", "partite", "partida", "partidas", "mecz", "mecze", "meczów",
        "potje", "potjes", "maç", "경기", "场比赛",
        "матч", "матчі", "матчів", "match", "matcher", "zápas", "zápasy", "zápasů"));

    /// <summary>"3 fichiers" / "3 files" / …</summary>
    public static string Files(int n) => Count(n, "file", new("fichier", "fichiers", "file", "files", "archivo", "archivos",
        "Datei", "Dateien", "файл", "файла", "файлов", "件",
        "file", "file", "arquivo", "arquivos", "plik", "pliki", "plików",
        "bestand", "bestanden", "dosya", "개 파일", "个文件",
        "файл", "файли", "файлів", "fil", "filer", "soubor", "soubory", "souborů"));

    /// <summary>"3 trésors obtenus" / "3 treasures" / …</summary>
    public static string TreasuresObtained(int n) => Count(n, "treasure", new("trésor obtenu", "trésors obtenus", "treasure", "treasures",
        "tesoro obtenido", "tesoros obtenidos", "Schatz erhalten", "Schätze erhalten", "сокровище получено", "сокровища получено",
        "сокровищ получено", "個の宝を入手",
        "tesoro ottenuto", "tesori ottenuti", "tesouro obtido", "tesouros obtidos", "skarb zdobyty", "skarby zdobyte", "skarbów zdobytych",
        "schat verkregen", "schatten verkregen", "hazine elde edildi", "개의 보물 획득", "个宝藏",
        "скарб отримано", "скарби отримано", "скарбів отримано", "skatt erhållen", "skatter erhållna",
        "poklad získán", "poklady získány", "pokladů získáno"));

    /// <summary>"12e" / "12th" / "12.º" / "12." / "12-е" / "12位" / "12º" / "12위" / "第12名" / "12:e"…</summary>
    public static string Ordinal(int n)
    {
        switch (Code)
        {
            case "es": return $"{n}.º";
            case "de" or "pl" or "tr" or "cs": return $"{n}.";
            case "ru" or "uk": return $"{n}-е";
            case "ja": return $"{n}位";
            case "it" or "pt": return $"{n}º";
            case "nl": return $"{n}e";
            case "ko": return $"{n}위";
            case "zh": return $"第{n}名";
            case "sv": return $"{n}:{((n % 10) is 1 or 2 && (n % 100) is not (11 or 12) ? "a" : "e")}";
            case "fr": return n == 1 ? "1er" : $"{n}e";
            case var _ when IsExtra && LangExtra.Get("#ordinal") is { } ordinal: return ordinal.Replace("{0}", n.ToString(CultureInfo.InvariantCulture));
        }
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>Short date + time of a match ("05/11 22:54" / "5 Nov 22:54" / "05.11. 22:54" / "11/05 22:54"…).</summary>
    public static string DateTimeShort(DateTime d) =>
        d.ToString(T("dd/MM HH:mm", "d MMM HH:mm", "dd/MM HH:mm", "dd.MM. HH:mm", "dd.MM HH:mm", "MM/dd HH:mm",
            "dd/MM HH:mm", "dd/MM HH:mm", "dd.MM HH:mm", "dd-MM HH:mm", "dd.MM HH:mm", "MM.dd HH:mm", "MM/dd HH:mm",
            "dd.MM HH:mm", "d MMM HH:mm", "d. M. HH:mm"), Culture);

    // French names stored in data.json -> display names in the other languages, in the order of T (en … cs).
    private static readonly Dictionary<string, string[]> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Coffre"] = new[] { "Chest", "Cofre", "Truhe", "Сундук", "宝箱", "Forziere", "Baú", "Skrzynia", "Kist", "Sandık", "상자", "宝箱", "Скриня", "Kista", "Truhla" },
        ["Coffre rare"] = new[] { "Rare chest", "Cofre raro", "Seltene Truhe", "Редкий сундук", "レア宝箱", "Forziere raro", "Baú raro", "Rzadka skrzynia", "Zeldzame kist", "Nadir sandık", "희귀 상자", "稀有宝箱", "Рідкісна скриня", "Sällsynt kista", "Vzácná truhla" },
        ["Coffre enterré"] = new[] { "Buried chest", "Cofre enterrado", "Vergrabene Truhe", "Зарытый сундук", "埋められた宝箱", "Forziere sepolto", "Baú enterrado", "Zakopana skrzynia", "Begraven kist", "Gömülü sandık", "묻힌 상자", "埋藏的宝箱", "Закопана скриня", "Begravd kista", "Zakopaná truhla" },
        ["Coffre d'extraction / reliques"] = new[] { "Extraction / relic chest", "Cofre de extracción / reliquias", "Extraktions-/Reliktruhe", "Сундук извлечения / реликвий", "抽出 / レリック宝箱", "Forziere di estrazione / reliquie", "Baú de extração / relíquias", "Skrzynia ekstrakcji / relikwii", "Extractie-/relikwiekist", "Çıkarma / kalıntı sandığı", "추출 / 유물 상자", "提取 / 遗物宝箱", "Скриня вилучення / реліквій", "Extraktions-/reliktkista", "Truhla extrakce / relikvií" },
        ["Coffre des affiches d'esprit"] = new[] { "Sprite poster chest", "Cofre de los carteles de espíritu", "Geisterplakat-Truhe", "Сундук с плакатами духов", "スプライトポスターの宝箱", "Forziere dei poster degli spiriti", "Baú dos pôsteres de espírito", "Skrzynia plakatów duszków", "Geestposterkist", "Ruh posteri sandığı", "스프라이트 포스터 상자", "精灵海报宝箱", "Скриня з плакатами духів", "Andeaffischkista", "Truhla plakátů skřítků" },
        ["Autre coffre"] = new[] { "Other chest", "Otro cofre", "Andere Truhe", "Другой сундук", "その他の宝箱", "Altro forziere", "Outro baú", "Inna skrzynia", "Andere kist", "Diğer sandık", "기타 상자", "其他宝箱", "Інша скриня", "Annan kista", "Jiná truhla" },
        ["Canon à butin"] = new[] { "Loot cannon", "Cañón de botín", "Beutekanone", "Пушка с добычей", "戦利品キャノン", "Cannone del bottino", "Canhão de saque", "Działo z łupem", "Buitkanon", "Ganimet topu", "전리품 대포", "战利品大炮", "Гармата зі здобиччю", "Bytekanon", "Kořistní dělo" },
        ["Caisse de munitions"] = new[] { "Ammo box", "Caja de munición", "Munitionskiste", "Ящик с боеприпасами", "弾薬箱", "Cassa di munizioni", "Caixa de munição", "Skrzynka z amunicją", "Munitiekist", "Cephane kutusu", "탄약 상자", "弹药箱", "Ящик з набоями", "Ammunitionslåda", "Bedna s municí" },
        ["Caisse de nourriture"] = new[] { "Food box", "Caja de comida", "Lebensmittelkiste", "Ящик с едой", "食料箱", "Cassa di cibo", "Caixa de comida", "Skrzynka z jedzeniem", "Voedselkist", "Yiyecek kutusu", "음식 상자", "食物箱", "Ящик з їжею", "Matlåda", "Bedna s jídlem" },
        ["Autre conteneur"] = new[] { "Other container", "Otro contenedor", "Anderer Behälter", "Другой контейнер", "その他のコンテナ", "Altro contenitore", "Outro contêiner", "Inny pojemnik", "Andere container", "Diğer kap", "기타 컨테이너", "其他容器", "Інший контейнер", "Annan behållare", "Jiný kontejner" },
        ["Trésor"] = new[] { "Treasure", "Tesoro", "Schatz", "Сокровище", "宝", "Tesoro", "Tesouro", "Skarb", "Schat", "Hazine", "보물", "宝藏", "Скарб", "Skatt", "Poklad" },
        ["Trésor (type inconnu)"] = new[] { "Treasure gizmo", "Artilugio de tesoro", "Schatz-Gizmo", "Устройство сокровища", "宝のギズモ", "Congegno del tesoro", "Dispositivo de tesouro", "Gadżet skarbu", "Schatgadget", "Hazine düzeneği", "보물 장치", "宝藏装置", "Пристрій скарбу", "Skattpryl", "Pokladové zařízení" },
        ["Gizmo de trésor"] = new[] { "Treasure gizmo", "Artilugio de tesoro", "Schatz-Gizmo", "Устройство сокровища", "宝のギズモ", "Congegno del tesoro", "Dispositivo de tesouro", "Gadżet skarbu", "Schatgadget", "Hazine düzeneği", "보물 장치", "宝藏装置", "Пристрій скарбу", "Skattpryl", "Pokladové zařízení" },
        ["Feu d'artifice doré"] = new[] { "Golden firework", "Fuego artificial dorado", "Goldenes Feuerwerk", "Золотой фейерверк", "ゴールデン花火", "Fuoco d'artificio dorato", "Fogos de artifício dourados", "Złoty fajerwerk", "Gouden vuurwerk", "Altın havai fişek", "황금 불꽃놀이", "黄金烟花", "Золотий феєрверк", "Gyllene fyrverkeri", "Zlatý ohňostroj" },
        ["Extracteur portable"] = new[] { "Portable extractor", "Extractor portátil", "Tragbarer Extraktor", "Переносной экстрактор", "ポータブル抽出機", "Estrattore portatile", "Extrator portátil", "Przenośny ekstraktor", "Draagbare extractor", "Taşınabilir çıkarıcı", "휴대용 추출기", "便携式提取器", "Переносний екстрактор", "Bärbar extraktor", "Přenosný extraktor" },
        ["Localisateur d'esprit"] = new[] { "Sprite locator", "Localizador de espíritus", "Geisterorter", "Локатор духов", "スプライトロケーター", "Localizzatore di spiriti", "Localizador de espíritos", "Lokalizator duszków", "Geestzoeker", "Ruh bulucu", "스프라이트 탐지기", "精灵定位器", "Локатор духів", "Andelokaliserare", "Lokátor skřítků" },
        ["Localisateur chanceux"] = new[] { "Lucky locator", "Localizador de la suerte", "Glücksorter", "Счастливый локатор", "ラッキーロケーター", "Localizzatore fortunato", "Localizador da sorte", "Szczęśliwy lokalizator", "Gelukszoeker", "Şanslı bulucu", "행운의 탐지기", "幸运定位器", "Щасливий локатор", "Turlokaliserare", "Šťastný lokátor" },
        ["Accélérateur d'extraction"] = new[] { "Extraction accelerator", "Acelerador de extracción", "Extraktionsbeschleuniger", "Ускоритель извлечения", "抽出アクセラレーター", "Acceleratore di estrazione", "Acelerador de extração", "Akcelerator ekstrakcji", "Extractieversneller", "Çıkarma hızlandırıcı", "추출 가속기", "提取加速器", "Прискорювач вилучення", "Extraktionsaccelerator", "Urychlovač extrakce" },
        ["inconnu"] = new[] { "unknown", "desconocido", "unbekannt", "неизвестно", "不明", "sconosciuto", "desconhecido", "nieznany", "onbekend", "bilinmiyor", "알 수 없음", "未知", "невідомо", "okänd", "neznámý" },
    };

    /// <summary>Display name of a chest kind or treasure item stored in French.</summary>
    public static string Name(string? stored)
    {
        if (string.IsNullOrEmpty(stored) || Code == "fr" || !Names.TryGetValue(stored, out var t))
        {
            return stored ?? "";
        }
        return T(stored, t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10], t[11], t[12], t[13], t[14]);
    }
}
