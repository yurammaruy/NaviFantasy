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
            "Navi Fantasy: /navi (configurações), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record",
            "Navi Fantasy: /navi (settings), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record",
            "Navi Fantasy: /navi（設定）、/navi status、/navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>、/navi roulettes、/navi mail、/navi radar、/navi record",
            "Navi Fantasy: /navi (Einstellungen), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record",
            "Navi Fantasy : /navi (paramètres), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record",
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
            "Comandos: /navi (configurações), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record, /ykff14",
            "Commands: /navi (settings), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record, /ykff14",
            "コマンド: /navi（設定）、/navi status、/navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>、/navi roulettes、/navi mail、/navi radar、/navi record、/ykff14",
            "Befehle: /navi (Einstellungen), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record, /ykff14",
            "Commandes : /navi (paramètres), /navi status, /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>, /navi roulettes, /navi mail, /navi radar, /navi record, /ykff14",
        },
        ["test_usage"] = new[]
        {
            "Use: /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>",
            "Usage: /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>",
            "使い方: /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>",
            "Verwendung: /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>",
            "Utilisation : /navi test <hp|mp|heal|tank|boss|danger|fact|daily|mail|water>",
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

        // ------------------------------------------------------------------ boss helper: messages
        ["boss_cast_tip"] = new[]
        {
            "{0} está preparando {1}! {2}",
            "{0} is preparing {1}! {2}",
            "{0}が{1}を準備しているよ！{2}",
            "{0} bereitet {1} vor! {2}",
            "{0} prépare {1} ! {2}",
        },
        ["danger_now"] = new[]
        {
            "{0}, saia daí! {1}",
            "{0}, get out! {1}",
            "{0}、そこから出て！{1}",
            "{0}, raus da! {1}",
            "{0}, sors de là ! {1}",
        },
        ["tip_circle_caster"] = new[]
        {
            "Afaste-se do chefe: a área em volta dele será atingida.",
            "Move away from the boss: the area around it will be hit.",
            "ボスから離れて。周囲が攻撃されるよ。",
            "Geh weg vom Boss: Der Bereich um ihn herum wird getroffen.",
            "Éloigne-toi du boss : la zone autour de lui sera touchée.",
        },
        ["adv_circle_caster"] = new[]
        {
            "Afaste-se do chefe, pelo menos {0} m.",
            "Move away from the boss, at least {0} m.",
            "ボスから{0}m以上離れて！",
            "Geh mindestens {0} m vom Boss weg!",
            "Éloigne-toi du boss d'au moins {0} m !",
        },
        ["tip_circle_target_me"] = new[]
        {
            "Uma área vai cair em você: afaste-se dos aliados.",
            "An area will land on you: move away from your allies.",
            "あなたの足元に範囲攻撃が来るよ。仲間から離れて。",
            "Ein Bereich landet auf dir: Geh weg von deinen Verbündeten.",
            "Une zone va tomber sur toi : éloigne-toi de tes alliés.",
        },
        ["tip_circle_target_other"] = new[]
        {
            "Uma área vai atingir {0}: não fique perto dele.",
            "An area will hit {0}: stay away from them.",
            "{0}に範囲攻撃が来るよ。近づかないで。",
            "Ein Bereich trifft {0}: Halte Abstand.",
            "Une zone va toucher {0} : reste à distance.",
        },
        ["adv_circle_target_other"] = new[]
        {
            "Afaste-se de {0}!",
            "Move away from {0}!",
            "{0}から離れて！",
            "Geh weg von {0}!",
            "Éloigne-toi de {0} !",
        },
        ["tip_line"] = new[]
        {
            "Linha à frente do chefe: saia da frente dele, vá para o lado.",
            "A line in front of the boss: leave its front and step to the side.",
            "ボスの正面に直線攻撃。正面を外れて横に動いて。",
            "Linie vor dem Boss: Verlasse die Vorderseite und geh zur Seite.",
            "Ligne devant le boss : quitte sa face et déplace-toi sur le côté.",
        },
        ["adv_line"] = new[]
        {
            "Saia da frente do chefe: vá para o lado!",
            "Get out of the boss's front: step to the side!",
            "ボスの正面から外れて、横へ！",
            "Raus aus der Front des Bosses: Geh zur Seite!",
            "Quitte la face du boss : déplace-toi sur le côté !",
        },
        ["tip_cone"] = new[]
        {
            "Ataque em leque na frente do chefe: fique atrás ou ao lado dele.",
            "A cone attack in front of the boss: stay behind or beside it.",
            "ボスの前方に扇状の攻撃。後ろか横にいて。",
            "Kegelangriff vor dem Boss: Bleib hinter oder neben ihm.",
            "Attaque en cône devant le boss : reste derrière ou sur le côté.",
        },
        ["tip_raidwide_healer"] = new[]
        {
            "Dano em todos: prepare a cura em grupo.",
            "Damage to everyone: get your group healing ready.",
            "全体攻撃。全体回復の準備を。",
            "Schaden für alle: Bereite Gruppenheilung vor.",
            "Dégâts sur tous : prépare un soin de groupe.",
        },
        ["tip_raidwide_other"] = new[]
        {
            "Dano em todos: use uma defesa se precisar.",
            "Damage to everyone: use a defensive if you need it.",
            "全体攻撃。必要なら防御を使って。",
            "Schaden für alle: Nutze bei Bedarf eine Verteidigungsfähigkeit.",
            "Dégâts sur tous : utilise une défense si besoin.",
        },
        ["tip_buster_me_tank"] = new[]
        {
            "Golpe forte em você: use uma defesa!",
            "Heavy hit on you: use a defensive!",
            "あなたに強攻撃。防御を使って！",
            "Schwerer Treffer auf dich: Nutze eine Verteidigung!",
            "Gros coup sur toi : utilise une défense !",
        },
        ["tip_buster_me_other"] = new[]
        {
            "Golpe forte em você: use uma defesa ou fique perto do healer.",
            "Heavy hit on you: use a defensive or stay near the healer.",
            "あなたに強攻撃。防御を使うか、ヒーラーの近くにいて。",
            "Schwerer Treffer auf dich: Nutze eine Verteidigung oder bleib in der Nähe des Heilers.",
            "Gros coup sur toi : utilise une défense ou reste près du soigneur.",
        },
        ["tip_buster_other_healer"] = new[]
        {
            "{0} vai receber um golpe forte: prepare a cura.",
            "{0} is about to take a heavy hit: get a heal ready.",
            "{0}に強攻撃が来るよ。回復の準備を。",
            "{0} bekommt gleich einen schweren Treffer: Bereite eine Heilung vor.",
            "{0} va subir un gros coup : prépare un soin.",
        },
        ["tip_unknown_area"] = new[]
        {
            "Olhe o desenho vermelho no chão e saia da área.",
            "Look for the red marking on the floor and step out of the area.",
            "床の赤い範囲を確認して、そこから出て。",
            "Achte auf die rote Markierung am Boden und verlasse den Bereich.",
            "Repère la zone rouge au sol et sors de la zone.",
        },

        // ------------------------------------------------------------------ boss helper: settings and commands
        ["tab_general"] = new[] { "Geral", "General", "一般", "Allgemein", "Général" },
        ["tab_boss"] = new[] { "Radar de chefes", "Boss radar", "ボスレーダー", "Boss-Radar", "Radar de boss" },
        ["cfg_boss_master"] = new[]
        {
            "Ativar a ajuda em lutas contra chefes",
            "Enable boss fight help",
            "ボス戦のサポートを有効にする",
            "Boss-Kampfhilfe aktivieren",
            "Activer l'aide en combat de boss",
        },
        ["cfg_radar_window"] = new[]
        {
            "Mostrar a janela do radar",
            "Show the radar window",
            "レーダーウィンドウを表示",
            "Radar-Fenster anzeigen",
            "Afficher la fenêtre du radar",
        },
        ["cfg_radar_aoe"] = new[]
        {
            "Mostrar as áreas de ataque no radar",
            "Show attack areas on the radar",
            "レーダーに攻撃範囲を表示",
            "Angriffsbereiche im Radar anzeigen",
            "Afficher les zones d'attaque sur le radar",
        },
        ["cfg_radar_floor"] = new[]
        {
            "Desenhar as áreas de ataque no chão (experimental)",
            "Draw attack areas on the floor (experimental)",
            "床に攻撃範囲を描画（試験的）",
            "Angriffsbereiche auf dem Boden zeichnen (experimentell)",
            "Dessiner les zones d'attaque au sol (expérimental)",
        },
        ["cfg_radar_range"] = new[]
        {
            "Alcance do radar (m)",
            "Radar range (m)",
            "レーダー範囲（m）",
            "Radar-Reichweite (m)",
            "Portée du radar (m)",
        },
        ["cfg_radar_size"] = new[]
        {
            "Tamanho do radar",
            "Radar size",
            "レーダーのサイズ",
            "Radar-Größe",
            "Taille du radar",
        },
        ["cfg_alert_upcoming"] = new[]
        {
            "Avisar qual mecânica vem a seguir e como lidar",
            "Announce the next mechanic and how to handle it",
            "次のギミックと対処法を知らせる",
            "Nächste Mechanik und den Umgang damit ansagen",
            "Annoncer la prochaine mécanique et comment la gérer",
        },
        ["cfg_alert_danger"] = new[]
        {
            "Avisar quando eu estiver em perigo",
            "Warn me when I am in danger",
            "危険な場所にいるとき警告する",
            "Warnen, wenn ich in Gefahr bin",
            "Me prévenir quand je suis en danger",
        },
        ["cfg_alert_raidwide"] = new[]
        {
            "Avisar dano em todos (raidwide)",
            "Warn about damage to everyone (raidwide)",
            "全体攻撃を警告する",
            "Vor Schaden für alle warnen (Raidwide)",
            "Prévenir des dégâts sur tous (raidwide)",
        },
        ["cfg_alert_buster"] = new[]
        {
            "Avisar golpes fortes em um alvo só (tank buster)",
            "Warn about heavy single-target hits (tank buster)",
            "単体への強攻撃を警告する（タンクバスター）",
            "Vor schweren Einzelzielangriffen warnen (Tank-Buster)",
            "Prévenir des gros coups sur une cible (tank buster)",
        },
        ["cfg_boss_note"] = new[]
        {
            "As dicas são genéricas, por tipo de ataque. Dicas específicas de cada chefe podem ser acrescentadas em Assets/mechanics.json. Use /navi record para gravar os golpes de uma luta.",
            "The tips are generic, by attack type. Boss-specific tips can be added in Assets/mechanics.json. Use /navi record to record the attacks of a fight.",
            "ヒントは攻撃の種類ごとの一般的なものです。ボス専用のヒントは Assets/mechanics.json に追加できます。/navi record で戦闘中の攻撃を記録できます。",
            "Die Tipps sind allgemein, je nach Angriffsart. Boss-spezifische Tipps lassen sich in Assets/mechanics.json ergänzen. Mit /navi record werden die Angriffe eines Kampfes aufgezeichnet.",
            "Les conseils sont génériques, selon le type d'attaque. Des conseils propres à chaque boss peuvent être ajoutés dans Assets/mechanics.json. Utilise /navi record pour enregistrer les attaques d'un combat.",
        },
        ["radar_on"] = new[] { "Radar ligado.", "Radar on.", "レーダーをオンにしました。", "Radar an.", "Radar activé." },
        ["radar_off"] = new[] { "Radar desligado.", "Radar off.", "レーダーをオフにしました。", "Radar aus.", "Radar désactivé." },
        ["record_on"] = new[]
        {
            "Gravando os golpes dos inimigos em: {0}",
            "Recording enemy attacks to: {0}",
            "敵の攻撃を記録中: {0}",
            "Angriffe der Gegner werden aufgezeichnet in: {0}",
            "Enregistrement des attaques ennemies dans : {0}",
        },
        ["record_off"] = new[]
        {
            "Gravação dos golpes desligada.",
            "Attack recording off.",
            "攻撃の記録をオフにしました。",
            "Aufzeichnung der Angriffe aus.",
            "Enregistrement des attaques désactivé.",
        },

        // ------------------------------------------------------------------ the fairy learns each boss
        ["learn_first"] = new[]
        {
            "Essa é minha primeira vez contra {0}. Estou com medo, mas vou dar o meu melhor!",
            "This is my first time against {0}. I'm scared, but I'll do my best!",
            "{0}と戦うのは初めてだよ。怖いけど、がんばるね！",
            "Das ist mein erstes Mal gegen {0}. Ich habe Angst, aber ich gebe mein Bestes!",
            "C'est ma première fois contre {0}. J'ai peur, mais je vais faire de mon mieux !",
        },
        ["learn_remember"] = new[]
        {
            "Eu me lembro de {0}! Agora consigo te avisar o que vem a seguir.",
            "I remember {0}! Now I can warn you about what comes next.",
            "{0}のこと、覚えてるよ！次に何が来るか教えられるね。",
            "Ich erinnere mich an {0}! Jetzt kann ich dich warnen, was als Nächstes kommt.",
            "Je me souviens de {0} ! Maintenant je peux t'avertir de ce qui arrive ensuite.",
        },
        ["pred_sure"] = new[]
        {
            "Em uns {0} s vem {1}. {2}",
            "In about {0} s: {1}. {2}",
            "約{0}秒後に{1}が来るよ。{2}",
            "In etwa {0} s kommt {1}. {2}",
            "Dans environ {0} s : {1}. {2}",
        },
        ["pred_maybe"] = new[]
        {
            "Se bem me lembro, em uns {0} s vem {1}. {2}",
            "If I remember right, in about {0} s: {1}. {2}",
            "たしか約{0}秒後に{1}が来るはず。{2}",
            "Wenn ich mich recht erinnere, kommt in etwa {0} s {1}. {2}",
            "Si je me souviens bien, dans environ {0} s : {1}. {2}",
        },
        ["tip_buster_pred_healer"] = new[]
        {
            "Golpe forte em um alvo: prepare a cura.",
            "Heavy hit on one target: get a heal ready.",
            "単体への強攻撃。回復の準備を。",
            "Schwerer Treffer auf ein Ziel: Bereite eine Heilung vor.",
            "Gros coup sur une cible : prépare un soin.",
        },
        ["tip_buster_pred_other"] = new[]
        {
            "Golpe forte em um alvo: se for em você, use uma defesa.",
            "Heavy hit on one target: if it is you, use a defensive.",
            "単体への強攻撃。自分なら防御を使って。",
            "Schwerer Treffer auf ein Ziel: Wenn du es bist, nutze eine Verteidigung.",
            "Gros coup sur une cible : si c'est toi, utilise une défense.",
        },
        ["note_hurt"] = new[]
        {
            "Essa já te machucou {0} de {1} vezes.",
            "This one has hurt you {0} out of {1} times.",
            "これは{1}回中{0}回、あなたにダメージを与えているよ。",
            "Das hat dir schon {0} von {1} Mal wehgetan.",
            "Celle-ci t'a déjà blessé {0} fois sur {1}.",
        },
        ["note_killed"] = new[]
        {
            "Essa já te derrubou antes!",
            "This one has knocked you down before!",
            "これで前に倒されたことがあるよ！",
            "Das hat dich schon einmal umgehauen!",
            "Celle-ci t'a déjà mis à terre !",
        },
        ["cfg_learning"] = new[]
        {
            "A fada aprende cada chefe e melhora as dicas a cada luta",
            "The fairy learns each boss and improves her tips with every fight",
            "妖精はボスごとに学習し、戦うたびにヒントが良くなる",
            "Die Fee lernt jeden Boss kennen und verbessert ihre Tipps mit jedem Kampf",
            "La fée apprend chaque boss et améliore ses conseils à chaque combat",
        },
        ["cfg_predict"] = new[]
        {
            "Avisar o golpe antes de ele começar (com base no que ela aprendeu)",
            "Announce the attack before it starts (based on what she learned)",
            "学習した内容をもとに、攻撃が始まる前に知らせる",
            "Angriff vor dem Start ansagen (anhand des Gelernten)",
            "Annoncer l'attaque avant qu'elle ne commence (selon ce qu'elle a appris)",
        },
        ["cfg_forget"] = new[]
        {
            "Apagar tudo o que ela aprendeu",
            "Erase everything she has learned",
            "学習した内容をすべて消す",
            "Alles Gelernte löschen",
            "Effacer tout ce qu'elle a appris",
        },
        ["cfg_forget_confirm"] = new[]
        {
            "Clique de novo para confirmar",
            "Click again to confirm",
            "もう一度クリックして確定",
            "Zur Bestätigung erneut klicken",
            "Clique à nouveau pour confirmer",
        },
        ["forget_done"] = new[]
        {
            "Tudo o que a fada aprendeu foi apagado.",
            "Everything the fairy learned has been erased.",
            "妖精が学習した内容をすべて消しました。",
            "Alles, was die Fee gelernt hat, wurde gelöscht.",
            "Tout ce que la fée a appris a été effacé.",
        },
        ["status_learned"] = new[]
        {
            "Chefes que ela já conhece: {0}",
            "Bosses she already knows: {0}",
            "覚えているボス: {0}体",
            "Bereits bekannte Bosse: {0}",
            "Boss déjà connus : {0}",
        },

        // ------------------------------------------------------------------ ground areas + what worked last time
        ["tip_circle_ground"] = new[]
        {
            "Uma área vai cair no chão: saia da marca vermelha.",
            "An area will land on the floor: step out of the red marking.",
            "床に範囲攻撃が来るよ。赤い範囲から出て。",
            "Ein Bereich landet auf dem Boden: Verlasse die rote Markierung.",
            "Une zone va tomber au sol : sors de la marque rouge.",
        },
        ["adv_circle_ground"] = new[]
        {
            "Saia da marca no chão!",
            "Get out of the marking on the floor!",
            "床の範囲から出て！",
            "Raus aus der Markierung am Boden!",
            "Sors de la marque au sol !",
        },
        ["learn_stack"] = new[]
        {
            "Da última vez, ficar perto de {0} funcionou.",
            "Last time, staying close to {0} worked.",
            "前回は{0}の近くにいてうまくいったよ。",
            "Letztes Mal hat es geholfen, bei {0} zu bleiben.",
            "La dernière fois, rester près de {0} a fonctionné.",
        },
        ["learn_avoid"] = new[]
        {
            "Da última vez, ficar longe de {0} funcionou.",
            "Last time, staying away from {0} worked.",
            "前回は{0}から離れていてうまくいったよ。",
            "Letztes Mal hat es geholfen, Abstand zu {0} zu halten.",
            "La dernière fois, rester loin de {0} a fonctionné.",
        },
        ["learn_behind"] = new[]
        {
            "Da última vez, ficar atrás ou ao lado do chefe funcionou.",
            "Last time, staying behind or beside the boss worked.",
            "前回はボスの後ろか横にいてうまくいったよ。",
            "Letztes Mal hat es geholfen, hinter oder neben dem Boss zu bleiben.",
            "La dernière fois, rester derrière ou sur le côté du boss a fonctionné.",
        },
        ["learn_far"] = new[]
        {
            "Da última vez, ficar a mais de {0} m do chefe funcionou.",
            "Last time, staying more than {0} m from the boss worked.",
            "前回はボスから{0}m以上離れていてうまくいったよ。",
            "Letztes Mal hat es geholfen, mehr als {0} m vom Boss entfernt zu bleiben.",
            "La dernière fois, rester à plus de {0} m du boss a fonctionné.",
        },
        ["learn_near"] = new[]
        {
            "Da última vez, ficar a menos de {0} m do chefe funcionou.",
            "Last time, staying within {0} m of the boss worked.",
            "前回はボスから{0}m以内にいてうまくいったよ。",
            "Letztes Mal hat es geholfen, innerhalb von {0} m vom Boss zu bleiben.",
            "La dernière fois, rester à moins de {0} m du boss a fonctionné.",
        },
        ["learn_spot"] = new[]
        {
            "Da última vez você ficou {0}, a uns {1} m, e não tomou dano.",
            "Last time you stood {0}, about {1} m away, and took no damage.",
            "前回は{0}、約{1}mの位置にいて、ダメージを受けなかったよ。",
            "Letztes Mal standest du {0}, etwa {1} m entfernt, und hast keinen Schaden genommen.",
            "La dernière fois, tu étais {0}, à environ {1} m, et tu n'as pas pris de dégâts.",
        },
        ["sector_front"] = new[] { "na frente do chefe", "in front of the boss", "ボスの正面", "vor dem Boss", "devant le boss" },
        ["sector_behind"] = new[] { "atrás do chefe", "behind the boss", "ボスの後ろ", "hinter dem Boss", "derrière le boss" },
        ["sector_side"] = new[] { "ao lado do chefe", "beside the boss", "ボスの横", "neben dem Boss", "à côté du boss" },
        ["cfg_radar_safe"] = new[]
        {
            "Mostrar no radar onde você se salvou da última vez",
            "Show on the radar where you were safe last time",
            "前回無事だった場所をレーダーに表示",
            "Im Radar zeigen, wo du letztes Mal sicher warst",
            "Afficher sur le radar où tu étais en sécurité la dernière fois",
        },
    };
}
