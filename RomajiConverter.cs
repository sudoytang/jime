namespace Jime;

public static class RomajiConverter
{
    private static readonly Dictionary<string, string> Syllables = new()
    {
        ["a"]="あ",["i"]="い",["u"]="う",["e"]="え",["o"]="お",
        ["ka"]="か",["ki"]="き",["ku"]="く",["ke"]="け",["ko"]="こ",
        ["sa"]="さ",["shi"]="し",["si"]="し",["su"]="す",["se"]="せ",["so"]="そ",
        ["ta"]="た",["chi"]="ち",["ti"]="ち",["tsu"]="つ",["tu"]="つ",["te"]="て",["to"]="と",
        ["na"]="な",["ni"]="に",["nu"]="ぬ",["ne"]="ね",["no"]="の",
        ["ha"]="は",["hi"]="ひ",["fu"]="ふ",["hu"]="ふ",["he"]="へ",["ho"]="ほ",
        ["ma"]="ま",["mi"]="み",["mu"]="む",["me"]="め",["mo"]="も",
        ["ya"]="や",["yu"]="ゆ",["yo"]="よ",
        ["ra"]="ら",["ri"]="り",["ru"]="る",["re"]="れ",["ro"]="ろ",
        ["wa"]="わ",["wo"]="を",["n"]="ん",
        ["ga"]="が",["gi"]="ぎ",["gu"]="ぐ",["ge"]="げ",["go"]="ご",
        ["za"]="ざ",["ji"]="じ",["zi"]="じ",["zu"]="ず",["ze"]="ぜ",["zo"]="ぞ",
        ["da"]="だ",["di"]="ぢ",["du"]="づ",["de"]="で",["do"]="ど",
        ["ba"]="ば",["bi"]="び",["bu"]="ぶ",["be"]="べ",["bo"]="ぼ",
        ["pa"]="ぱ",["pi"]="ぴ",["pu"]="ぷ",["pe"]="ぺ",["po"]="ぽ",
        ["kya"]="きゃ",["kyu"]="きゅ",["kyo"]="きょ",["sha"]="しゃ",["shu"]="しゅ",["sho"]="しょ",
        ["cha"]="ちゃ",["chu"]="ちゅ",["cho"]="ちょ",["nya"]="にゃ",["nyu"]="にゅ",["nyo"]="にょ",
        ["hya"]="ひゃ",["hyu"]="ひゅ",["hyo"]="ひょ",["mya"]="みゃ",["myu"]="みゅ",["myo"]="みょ",
        ["rya"]="りゃ",["ryu"]="りゅ",["ryo"]="りょ",["gya"]="ぎゃ",["gyu"]="ぎゅ",["gyo"]="ぎょ",
        ["ja"]="じゃ",["ju"]="じゅ",["jo"]="じょ",["jya"]="じゃ",["jyu"]="じゅ",["jyo"]="じょ",
        ["bya"]="びゃ",["byu"]="びゅ",["byo"]="びょ",["pya"]="ぴゃ",["pyu"]="ぴゅ",["pyo"]="ぴょ",
        ["fa"]="ふぁ",["fi"]="ふぃ",["fe"]="ふぇ",["fo"]="ふぉ",["va"]="ゔぁ",["vi"]="ゔぃ",["vu"]="ゔ",["ve"]="ゔぇ",["vo"]="ゔぉ",
        ["-"]="ー",["."]="。",[","]="、",["?"]="？",["!"]="！"
    };

    private static readonly string[] WordData =
    [
        "ohayou=おはよう", "ohayougozaimasu=おはようございます", "konnichiwa=こんにちは", "konbanwa=こんばんは",
        "arigatou=ありがとう", "arigatougozaimasu=ありがとうございます", "sumimasen=すみません", "gomennasai=ごめんなさい",
        "sayounara=さようなら", "oyasumi=おやすみ", "oyasuminasai=おやすみなさい", "onegai=お願い", "onegaishimasu=お願いします",
        "hai=はい", "iie=いいえ", "wakarimashita=分かりました", "wakatta=分かった", "shirimasen=知りません",
        "watashi=私", "watakushi=私", "anata=あなた", "kare=彼", "kanojo=彼女", "minna=みんな", "mina=皆",
        "minasan=皆さん", "sensei=先生", "gakusei=学生", "tomodachi=友達", "kazoku=家族", "chichi=父", "haha=母",
        "ani=兄", "ane=姉", "otouto=弟", "imouto=妹", "kodomo=子供", "hito=人", "namae=名前", "nihon=日本",
        "nihonjin=日本人", "nihongo=日本語", "chuugoku=中国", "chuugokujin=中国人", "chuugokugo=中国語", "eigo=英語",
        "toukyou=東京", "kyouto=京都", "oosaka=大阪", "tabemono=食べ物", "gohan=ご飯", "asagohan=朝ご飯",
        "hirugohan=昼ご飯", "bangohan=晩ご飯", "mizu=水", "ocha=お茶", "koucha=紅茶", "koohii=コーヒー",
        "sake=酒", "sushi=寿司", "ramen=ラーメン", "pan=パン", "niku=肉", "sakana=魚", "yasai=野菜",
        "taberu=食べる", "tabemasu=食べます", "nomu=飲む", "nomimasu=飲みます", "miru=見る", "mimasu=見ます",
        "kiku=聞く", "kikimasu=聞きます", "hanasu=話す", "hanashimasu=話します", "yomu=読む", "yomimasu=読みます",
        "kaku=書く", "kakimasu=書きます", "iku=行く", "ikimasu=行きます", "kuru=来る", "kimasu=来ます",
        "kaeru=帰る", "kaerimasu=帰ります", "aru=ある", "arimasu=あります", "iru=いる", "imasu=います",
        "suru=する", "shimasu=します", "dekiru=できる", "dekimasu=できます", "au=会う", "aimasu=会います",
        "kau=買う", "kaimasu=買います", "tsukau=使う", "tsukaimasu=使います", "hataraku=働く", "benkyou=勉強",
        "benkyoushimasu=勉強します", "shigoto=仕事", "kaisha=会社", "gakkou=学校", "daigaku=大学", "ie=家",
        "uchi=うち", "heya=部屋", "eki=駅", "densha=電車", "basu=バス", "kuruma=車", "jidousha=自動車",
        "jitensha=自転車", "hikouki=飛行機", "michi=道", "mise=店", "byouin=病院", "ginkou=銀行", "yuubinkyoku=郵便局",
        "hon=本", "shinbun=新聞", "zasshi=雑誌", "tegami=手紙", "denwa=電話", "keitai=携帯", "pasokon=パソコン",
        " terebi=テレビ", "eiga=映画", "ongaku=音楽", "uta=歌", "shashin=写真", "ryokou=旅行", "tenki=天気",
        "ame=雨", "yuki=雪", "kaze=風", "sora=空", "yama=山", "kawa=川", "umi=海", "hana=花",
        "ki=木", "inu=犬", "neko=猫", "tori=鳥", "ima=今", "kyou=今日", "ashita=明日", "kinou=昨日",
        "asa=朝", "hiru=昼", "ban=晩", "yoru=夜", "mainichi=毎日", "maishuu=毎週", "maitoshi=毎年",
        "getsuyoubi=月曜日", "kayoubi=火曜日", "suiyoubi=水曜日", "mokuyoubi=木曜日", "kinyoubi=金曜日",
        "doyoubi=土曜日", "nichiyoubi=日曜日", "jikan=時間", "toki=時", "kotoshi=今年", "rainen=来年",
        "kyonen=去年", "ichi=一", "ni=二", "san=三", "yon=四", "go=五", "roku=六", "nana=七", "hachi=八",
        "kyuu=九", "juu=十", "hyaku=百", "sen=千", "man=万", "en=円", "ooi=多い", "sukunai=少ない",
        "ookii=大きい", "chiisai=小さい", "takai=高い", "yasui=安い", "atarashii=新しい", "furui=古い",
        "ii=いい", "yoi=良い", "warui=悪い", "hayai=早い", "osoi=遅い", "atsui=暑い", "samui=寒い",
        "tanoshii=楽しい", "ureshii=嬉しい", "kanashii=悲しい", "isogashii=忙しい", "hima=暇", "genki=元気",
        "daijoubu=大丈夫", "kirei=綺麗", "benri=便利", "fuben=不便", "suki=好き", "kirai=嫌い", "daisuki=大好き",
        "desu=です", "masu=ます", "deshita=でした", "mashita=ました", "doko=どこ", "itsu=いつ", "dare=誰",
        "nani=何", "nan=何", "doushite=どうして", "naze=なぜ", "dou=どう", "kore=これ", "sore=それ", "are=あれ",
        "koko=ここ", "soko=そこ", "asoko=あそこ", "kochira=こちら", "sochira=そちら", "achira=あちら",
        "yoroshiku=よろしく", "yoroshikuonegaishimasu=よろしくお願いします", "otsukaresama=お疲れ様", "itadakimasu=いただきます"
    ];

    private static readonly (string Reading, string Word)[] Words = WordData
        .Select(x => x.Trim().Split('='))
        .Select(x => (x[0], x[1]))
        .ToArray();

    public static string ToHiragana(string romaji)
    {
        var source = romaji.ToLowerInvariant();
        var result = new System.Text.StringBuilder();
        var position = 0;
        while (position < source.Length)
        {
            var matched = false;
            for (var length = Math.Min(3, source.Length - position); length >= 1; length--)
            {
                var part = source.Substring(position, length);
                if (!Syllables.TryGetValue(part, out var kana)) continue;
                result.Append(kana);
                position += length;
                matched = true;
                break;
            }
            if (matched) continue;

            var current = source[position];
            if (position + 1 < source.Length && current == source[position + 1] && "bcdfghjklmpqrstvwxyz".Contains(current))
            {
                result.Append('っ');
                position++;
                continue;
            }
            if (current == 'n' && (position + 1 == source.Length || !"aiueoyn".Contains(source[position + 1])))
            {
                result.Append('ん');
                position++;
                continue;
            }
            result.Append(current);
            position++;
        }
        return result.ToString();
    }

    public static IReadOnlyList<string> SplitByKanaSegments(string romaji, IReadOnlyList<string> segments)
    {
        if (segments.Count < 2) return new[] { romaji };
        var source = romaji.ToLowerInvariant();
        var tokens = new List<(string Raw, string Kana)>();
        var position = 0;
        while (position < source.Length)
        {
            var matched = false;
            for (var length = Math.Min(3, source.Length - position); length >= 1; length--)
            {
                var part = source.Substring(position, length);
                if (!Syllables.TryGetValue(part, out var kana)) continue;
                tokens.Add((romaji.Substring(position, length), kana));
                position += length;
                matched = true;
                break;
            }
            if (matched) continue;
            var current = source[position];
            if (position + 1 < source.Length && current == source[position + 1] && "bcdfghjklmpqrstvwxyz".Contains(current))
            {
                tokens.Add((romaji[position].ToString(), "っ"));
                position++;
                continue;
            }
            if (current == 'n' && (position + 1 == source.Length || !"aiueoyn".Contains(source[position + 1])))
            {
                tokens.Add((romaji[position].ToString(), "ん"));
                position++;
                continue;
            }
            tokens.Add((romaji[position].ToString(), current.ToString()));
            position++;
        }

        var result = new List<string>();
        var tokenIndex = 0;
        foreach (var segment in segments)
        {
            var raw = new System.Text.StringBuilder();
            var kana = new System.Text.StringBuilder();
            while (tokenIndex < tokens.Count && kana.Length < segment.Length)
            {
                raw.Append(tokens[tokenIndex].Raw);
                kana.Append(tokens[tokenIndex].Kana);
                tokenIndex++;
            }
            if (kana.ToString() != segment) return new[] { romaji };
            result.Add(raw.ToString());
        }
        if (tokenIndex < tokens.Count)
            result.Add(string.Concat(tokens.Skip(tokenIndex).Select(t => t.Raw)));
        return result;
    }

    public static IReadOnlyList<string> GetCandidates(string romaji)
    {
        var hiragana = ToHiragana(romaji);
        var result = new List<string> { hiragana };
        var lower = romaji.ToLowerInvariant();
        foreach (var (reading, word) in Words)
        {
            if (reading.StartsWith(lower, StringComparison.Ordinal) && !result.Contains(word)) result.Add(word);
            if (result.Count == 20) break;
        }
        var katakana = ToKatakana(hiragana);
        if (katakana != hiragana) result.Add(katakana);
        return result;
    }

    public static string ToKatakana(string text)
    {
        var chars = text.Select(c => c is >= 'ぁ' and <= 'ゖ' ? (char)(c + 0x60) : c).ToArray();
        return new string(chars);
    }
}
