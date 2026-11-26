using System.Collections.Generic;
using System.Globalization;

namespace NaviFantasy;

public enum NaviLanguage
{
    Portuguese = 0,
    English = 1,
    Japanese = 2,
    German = 3,
    French = 4,
}

/// <summary>All the texts of the plugin, in the 4 game languages plus Brazilian Portuguese. Order: pt, en, ja, de, fr.</summary>
public static class Loc
{
    public static NaviLanguage Current { get; set; } = NaviLanguage.Portuguese;

    public static readonly NaviLanguage[] All =
    {
        NaviLanguage.Portuguese, NaviLanguage.English, NaviLanguage.Japanese, NaviLanguage.German, NaviLanguage.French,
    };

    public static string NativeName(NaviLanguage language) => language switch
    {
        NaviLanguage.Portuguese => "Português (BR)",
        NaviLanguage.English => "English",
        NaviLanguage.Japanese => "日本語",
        NaviLanguage.German => "Deutsch",
        NaviLanguage.French => "Français",
        _ => "English",
    };

    public static string Code(NaviLanguage language) => language switch
    {
        NaviLanguage.Portuguese => "pt",
        NaviLanguage.English => "en",
        NaviLanguage.Japanese => "ja",
        NaviLanguage.German => "de",
        NaviLanguage.French => "fr",
        _ => "en",
    };

    public static string Get(string key, params object[] args)
    {
        if (!Table.TryGetValue(key, out var forms))
            return key;

        var text = forms[(int)Current];
        if (string.IsNullOrEmpty(text))
            text = forms[(int)NaviLanguage.English];

        return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
    }

    private static readonly Dictionary<string, string[]> Table = new()
    {
        // ------------------------------------------------------------------ fairy messages
        ["hp_low"] = new[]
        {
            "{0}, seu HP está baixo!",
            "{0}, your HP is low!",
            "{0}、HPが低いよ！",
            "{0}, deine LP sind niedrig!",
            "{0}, tes PV sont bas !",
        },
        ["mp_low"] = new[]
        {
            "{0}, sua mana está baixa!",
            "{0}, your MP is low!",
            "{0}、MPが少ないよ！",
            "{0}, deine MP sind niedrig!",
            "{0}, tes PM sont bas !",
        },
        ["ally_heal"] = new[]
        {
            "{0} precisa de cura!",
            "{0} needs healing!",
            "{0}に回復が必要だよ！",
            "{0} braucht Heilung!",
            "{0} a besoin de soins !",
        },
        ["allies_hurt"] = new[]
        {
            "{0}, seus aliados estão sofrendo dano!",
            "{0}, your allies are taking damage!",
            "{0}、仲間がダメージを受けているよ！",
            "{0}, deine Verbündeten erleiden Schaden!",
            "{0}, tes alliés subissent des dégâts !",
        },
        ["boss_cast"] = new[]
        {
            "{0} está preparando {1}!",
            "{0} is preparing {1}!",
            "{0}が{1}を準備しているよ！",
            "{0} bereitet {1} vor!",
            "{0} prépare {1} !",
        },
        ["strong_attack"] = new[]
        {
            "um golpe forte",
            "a powerful attack",
            "強力な攻撃",
            "einen starken Angriff",
            "une attaque puissante",
        },
        ["daily"] = new[]
        {
            "{0}, você ainda tem atividades diárias: {1}!",
            "{0}, you still have daily activities to do: {1}!",
            "{0}、まだ今日のデイリーが残ってるよ：{1}！",
            "{0}, du hast noch tägliche Aufgaben: {1}!",
            "{0}, il te reste des activités quotidiennes : {1} !",
        },
        ["and_more"] = new[]
        {
            " e mais {0}",
            " and {0} more",
            " 他{0}件",
            " und {0} weitere",
            " et {0} autres",
        },
        ["mail"] = new[]
        {
            "{0}, você tem cartas no correio que ainda não leu!",
            "{0}, you have unread letters in your mailbox!",
            "{0}、未読の手紙が届いているよ！",
            "{0}, du hast ungelesene Briefe im Briefkasten!",
            "{0}, tu as des lettres non lues dans ta boîte aux lettres !",
        },
        ["hydration"] = new[]
        {
            "Seu personagem não precisa comer nem beber mas você sim, não queremos perder nenhum jogador dedicado.",
            "Your character doesn't need to eat or drink, but you do. We don't want to lose a single dedicated player.",
            "キャラクターは食べなくても飲まなくても平気だけど、あなたはそうじゃないよ。熱心なプレイヤーを失いたくないからね。",
            "Dein Charakter muss weder essen noch trinken, aber du schon. Wir wollen keinen engagierten Spieler verlieren.",
            "Ton personnage n'a besoin ni de manger ni de boire, mais toi, si. Nous ne voulons perdre aucun joueur dévoué.",
        },
        ["fact_prefix"] = new[]
        {
            "Você sabia?",
            "Did you know?",
            "知ってた？",
            "Wusstest du schon?",
            "Le saviez-vous ?",
        },

        // ------------------------------------------------------------------ settings window
        ["cfg_size"] = new[] { "Tamanho", "Size", "サイズ", "Größe", "Taille" },
        ["cfg_hp"] = new[]
        {
            "Alertar HP abaixo de",
            "Warn when HP is below",
            "HPが次の値を下回ったら通知",
            "Warnen, wenn LP unter",
            "Avertir si les PV sont sous",
        },
        ["cfg_mp"] = new[]
        {
            "Alertar mana abaixo de",
            "Warn when MP is below",
            "MPが次の値を下回ったら通知",
            "Warnen, wenn MP unter",
            "Avertir si les PM sont sous",
        },
        ["cfg_ally"] = new[]
        {
            "Healer: aliado precisa de cura abaixo de",
            "Healer: ally needs healing below",
            "ヒーラー：味方の回復が必要になる値",
            "Heiler: Verbündeter braucht Heilung unter",
            "Soigneur : un allié a besoin de soins sous",
        },
        ["cfg_dailies"] = new[]
        {
            "Lembrar das roletas diárias pendentes",
            "Remind me of pending daily roulettes",
            "未完了のデイリールーレットを通知",
            "An offene tägliche Zufallsinhalte erinnern",
            "Rappeler les roulettes quotidiennes en attente",
        },
        ["cfg_roulettes"] = new[]
        {
            "Quais roletas acompanhar",
            "Which roulettes to track",
            "対象のルーレット",
            "Welche Zufallsinhalte verfolgen",
            "Roulettes à suivre",
        },
        ["cfg_mail"] = new[]
        {
            "Avisar sobre cartas não lidas no correio",
            "Warn about unread letters in the mailbox",
            "未読の手紙を通知",
            "Vor ungelesenen Briefen warnen",
            "Prévenir en cas de lettres non lues",
        },
        ["cfg_water"] = new[]
        {
            "Lembrar de comer e beber (a cada 1h11)",
            "Remind me to eat and drink (every 1h11)",
            "食事と水分補給を通知（1時間11分ごと）",
            "Erinnerung ans Essen und Trinken (alle 1:11 h)",
            "Rappeler de manger et de boire (toutes les 1h11)",
        },
        ["cfg_language"] = new[]
        {
            "Idioma / Language",
            "Idioma / Language",
            "Idioma / Language",
            "Idioma / Language",
            "Idioma / Language",
        },

        // ------------------------------------------------------------------ chat commands
        ["cmd_help"] = new[]
        {
            "Navi Fantasy: /navi (configurações), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail",
            "Navi Fantasy: /navi (settings), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail",
            "Navi Fantasy: /navi（設定）、/navi status、/navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>、/navi roulettes、/navi mail",
            "Navi Fantasy: /navi (Einstellungen), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail",
            "Navi Fantasy : /navi (paramètres), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail",
        },
        ["cmd_fact_help"] = new[]
        {
            "A Navi conta agora uma curiosidade aleatória sobre FF14 (Você sabia?).",
            "Navi tells you a random FFXIV fun fact right now (You Know FF14).",
            "ナビがFF14の豆知識をランダムに今すぐ教えてくれる（You Know FF14）。",
            "Navi erzählt dir sofort eine zufällige FFXIV-Kuriosität (You Know FF14).",
            "Navi vous raconte tout de suite une anecdote aléatoire sur FF14 (You Know FF14).",
        },
        ["cmd_usage"] = new[]
        {
            "Comandos: /navi (configurações), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail, /ykff14",
            "Commands: /navi (settings), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail, /ykff14",
            "コマンド: /navi（設定）、/navi status、/navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>、/navi roulettes、/navi mail、/ykff14",
            "Befehle: /navi (Einstellungen), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail, /ykff14",
            "Commandes : /navi (paramètres), /navi status, /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>, /navi roulettes, /navi mail, /ykff14",
        },
        ["test_usage"] = new[]
        {
            "Use: /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>",
            "Usage: /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>",
            "使い方: /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>",
            "Verwendung: /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>",
            "Utilisation : /navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>",
        },
        ["pvp_off"] = new[]
        {
            "A fada está desativada no PvP.",
            "The fairy is disabled in PvP.",
            "PvPでは妖精は無効になっているよ。",
            "Die Fee ist im PvP deaktiviert.",
            "La fée est désactivée en PvP.",
        },
        ["no_char"] = new[]
        {
            "Nenhum personagem encontrado.",
            "No character found.",
            "キャラクターが見つかりません。",
            "Kein Charakter gefunden.",
            "Aucun personnage trouvé.",
        },
        ["no_facts"] = new[]
        {
            "Não encontrei nenhuma curiosidade (arquivos em Assets).",
            "No fun facts found (files in Assets).",
            "豆知識が見つかりません（Assetsのファイル）。",
            "Keine Kuriositäten gefunden (Dateien in Assets).",
            "Aucune anecdote trouvée (fichiers dans Assets).",
        },
        ["status_hello"] = new[]
        {
            "Olá, {0}! Você é {1} ({2}), função: {3}.",
            "Hello, {0}! You are {1} ({2}), role: {3}.",
            "こんにちは、{0}！あなたは{1}（{2}）、ロールは{3}だね。",
            "Hallo, {0}! Du bist {1} ({2}), Rolle: {3}.",
            "Bonjour, {0} ! Tu es {1} ({2}), rôle : {3}.",
        },
        ["role_tank"] = new[] { "Tank", "Tank", "タンク", "Tank", "Tank" },
        ["role_melee"] = new[] { "DPS corpo a corpo", "Melee DPS", "近接DPS", "Nahkampf-DPS", "DPS mêlée" },
        ["role_ranged"] = new[] { "DPS à distância", "Ranged DPS", "遠隔DPS", "Fernkampf-DPS", "DPS à distance" },
        ["role_healer"] = new[] { "Healer", "Healer", "ヒーラー", "Heiler", "Soigneur" },
        ["role_none"] = new[]
        {
            "sem função de combate",
            "no combat role",
            "戦闘ロールなし",
            "keine Kampfrolle",
            "aucun rôle de combat",
        },
        ["status_daily"] = new[]
        {
            "Roletas diárias pendentes: {0}",
            "Pending daily roulettes: {0}",
            "未完了のデイリールーレット: {0}",
            "Offene tägliche Zufallsinhalte: {0}",
            "Roulettes quotidiennes en attente : {0}",
        },
        ["none_word"] = new[] { "nenhuma", "none", "なし", "keine", "aucune" },
        ["status_area"] = new[]
        {
            "Território: tipo {0} | zona segura: {1} | contador de curiosidade: {2}/60 min",
            "Territory: type {0} | safe area: {1} | fun fact timer: {2}/60 min",
            "エリア: タイプ{0} | 安全地帯: {1} | 豆知識タイマー: {2}/60分",
            "Gebiet: Typ {0} | sichere Zone: {1} | Kuriositäten-Timer: {2}/60 Min.",
            "Zone : type {0} | zone sûre : {1} | minuteur d'anecdote : {2}/60 min",
        },
        ["yes"] = new[] { "sim", "yes", "はい", "ja", "oui" },
        ["no"] = new[] { "não", "no", "いいえ", "nein", "non" },
        ["pvp_on"] = new[]
        {
            "sim (fada desativada)",
            "yes (fairy disabled)",
            "はい（妖精は無効）",
            "ja (Fee deaktiviert)",
            "oui (fée désactivée)",
        },
        ["status_vitals"] = new[]
        {
            "HP {0}/{1} | MP {2}/{3} | grupo: {4} | PvP: {5} | em combate: {6}",
            "HP {0}/{1} | MP {2}/{3} | party: {4} | PvP: {5} | in combat: {6}",
            "HP {0}/{1} | MP {2}/{3} | パーティ: {4} | PvP: {5} | 戦闘中: {6}",
            "LP {0}/{1} | MP {2}/{3} | Gruppe: {4} | PvP: {5} | im Kampf: {6}",
            "PV {0}/{1} | PM {2}/{3} | groupe : {4} | PvP : {5} | en combat : {6}",
        },
        ["roulettes_fail"] = new[]
        {
            "Não consegui carregar a lista de roletas.",
            "Could not load the roulette list.",
            "ルーレットの一覧を読み込めませんでした。",
            "Die Liste der Zufallsinhalte konnte nicht geladen werden.",
            "Impossible de charger la liste des roulettes.",
        },
        ["done_today"] = new[] { "concluída hoje", "done today", "本日完了", "heute erledigt", "terminée aujourd'hui" },
        ["pending_word"] = new[] { "pendente", "pending", "未完了", "offen", "en attente" },
        ["tracked_tag"] = new[] { " [acompanhada]", " [tracked]", " [対象]", " [verfolgt]", " [suivie]" },
        ["mail_none_seen"] = new[]
        {
            "Nenhum elemento de HUD com \"Letter\", \"Mail\" ou \"Notification\" no nome foi visto ainda (se o plugin foi instalado com o jogo aberto, relogue).",
            "No HUD element with \"Letter\", \"Mail\" or \"Notification\" in its name has been seen yet (if the plugin was installed while the game was running, log out and back in).",
            "名前に「Letter」「Mail」「Notification」を含むHUD要素はまだ見つかっていません（ゲーム起動中にプラグインを入れた場合は、ログインし直してください）。",
            "Es wurde noch kein HUD-Element mit \"Letter\", \"Mail\" oder \"Notification\" im Namen gesehen (wurde das Plugin bei laufendem Spiel installiert, bitte neu einloggen).",
            "Aucun élément d'interface avec « Letter », « Mail » ou « Notification » dans son nom n'a encore été vu (si le plugin a été installé pendant que le jeu tournait, reconnectez-vous).",
        },
        ["visible_word"] = new[] { "visível", "visible", "表示中", "sichtbar", "visible" },
        ["hidden_word"] = new[] { "oculto", "hidden", "非表示", "versteckt", "masqué" },
        ["mail_guess"] = new[]
        {
            "Cartas não lidas (palpite): {0}",
            "Unread letters (best guess): {0}",
            "未読の手紙（推測）: {0}",
            "Ungelesene Briefe (Schätzung): {0}",
            "Lettres non lues (estimation) : {0}",
        },
    };
}
