using System;
using System.Collections.Generic;
using System.Globalization;

namespace AsMobPlate.Localization;

public enum UiLanguage
{
    EN,
    JP,
    DE,
    FR,
}

public static class UiText
{
    // Column order is EN / JP / DE / FR. IDs remain independent of displayed translations.
    private static readonly Dictionary<string, string[]> Translations = new(StringComparer.Ordinal)
    {
        ["Minion"] = ["Minion", "配下", "Diener", "Serviteur"],
        ["Defeated"] = ["Defeated", "討伐済み", "Besiegt", "Vaincu"],
        ["Defeated elapsed"] = ["Defeated {0}s ago", "討伐から{0}秒", "Vor {0}s besiegt", "Vaincu il y a {0}s"],
        ["Show SS minions"] = ["Show SS minions", "SS配下を表示", "SS-Diener anzeigen", "Afficher les serviteurs SS"],
        ["Show SS boss"] = ["Show SS boss", "SS本体を表示", "SS-Boss anzeigen", "Afficher le boss SS"],
        ["Show hunt progress"] = ["Show hunt progress", "派生イベントの進行状況を表示", "Jagdfortschritt anzeigen", "Afficher la progression de la chasse"],
        ["Show defeated"] = ["Show defeated hunts", "討伐済みのモブを表示", "Besiegte Jagdziele anzeigen", "Afficher les cibles vaincues"],
        ["Defeated display duration"] = ["Defeated display duration", "討伐済みの表示時間", "Anzeigedauer nach dem Sieg", "Durée d'affichage après la victoire"],
        ["Progress panel X"] = ["Progress panel X", "進行パネルの横位置", "Fortschrittsanzeige X", "Position X de la progression"],
        ["Progress panel Y"] = ["Progress panel Y", "進行パネルの縦位置", "Fortschrittsanzeige Y", "Position Y de la progression"],
        ["Stage Searching"] = ["SS minions detected - searching", "SS配下出現・探索中", "SS-Diener erschienen - Suche", "Serviteurs SS apparus - recherche"],
        ["Stage Fighting"] = ["SS minion combat observed", "SS配下との交戦を確認", "Kampf mit SS-Dienern beobachtet", "Combat avec les serviteurs SS observé"],
        ["Stage Boss"] = ["SS boss detected", "SS本体出現", "SS-Boss entdeckt", "Boss SS détecté"],
        ["Stage Defeated"] = ["SS boss defeated", "SS討伐済み", "SS-Boss besiegt", "Boss SS vaincu"],
        ["Stage Returned"] = ["Minions returned", "配下帰還・派生終了", "Diener zurückgekehrt", "Serviteurs repartis"],
        ["Observed kills"] = ["Observed defeats: {0}/4", "観測済み討伐 {0}/4", "Beobachtete Siege: {0}/4", "Victoires observées : {0}/4"],
        ["Engagement guide"] = ["Engagement guideline: {0}s", "交戦開始の目安 あと{0}秒", "Richtwert für Kampfbeginn: {0}s", "Délai indicatif d'engagement : {0}s"],
        ["Awaiting confirmation"] = ["Awaiting outcome confirmation", "結果確認待ち", "Warten auf Ergebnisbestätigung", "En attente de confirmation du résultat"],
        ["Completion reported"] = ["Completion reported in chat (unconfirmed)", "チャットで終了報告あり（未確認）", "Ende im Chat gemeldet (unbestätigt)", "Fin signalée dans le chat (non confirmée)"],
        ["Show arrival warning"] = ["Show arrival warning", "到着予測・間に合わない警告を表示", "Ankunftswarnung anzeigen", "Afficher l'alerte d'arrivée"],
        ["Arrival distance"] = ["Arrival distance", "到着とみなす距離", "Ankunftsdistanz", "Distance d'arrivée"],
        ["Arrival preparation"] = ["Arrival preparation time", "到着後の準備時間", "Vorbereitungszeit", "Temps de préparation"],
        ["Arrival"] = ["Estimated arrival", "到着予測", "Geschätzte Ankunft", "Arrivée estimée"],
        ["Arrival too late"] = ["Likely too late", "間に合わない見込み", "Voraussichtlich zu spät", "Probablement trop tard"],
        ["Warning red"] = ["Warning stripe red", "警告ストライプ：赤", "Warnstreifen Rot", "Bande rouge d'alerte"],
        ["Warning yellow"] = ["Warning stripe yellow", "警告ストライプ：黄", "Warnstreifen Gelb", "Bande jaune d'alerte"],
        ["Preview"] = ["Preview", "プレビュー", "Vorschau", "Aperçu"],
        ["Preview idle"] = ["Idle", "通常", "Ruhezustand", "Au repos"],
        ["Preview countdown"] = ["Countdown", "カウントダウン", "Countdown", "Compte à rebours"],
        ["Preview HP"] = ["HP", "HP", "LP", "PV"],
        ["Animate preview"] = ["Animate countdown", "カウントダウンを試す", "Countdown animieren", "Animer le compte à rebours"],
        ["Preview remaining"] = ["Remaining seconds", "残り秒数", "Verbleibende Sekunden", "Secondes restantes"],
        ["Preview hunt name"] = ["Sample hunt", "サンプルモブ", "Beispiel-Jagdziel", "Cible de démonstration"],
        ["Preview rank hidden"] = ["This rank is hidden by the display settings.", "このランクは表示設定で非表示になっています。", "Dieser Rang ist in den Anzeigeeinstellungen ausgeblendet.", "Ce rang est masqué dans les paramètres d'affichage."],
        ["Display"] = ["Display", "表示", "Anzeige", "Affichage"],
        ["Notifications"] = ["Alerts", "通知", "Meldungen", "Alertes"],
        ["Timing"] = ["Start time", "開始時刻", "Startzeit", "Départ"],
        ["Appearance"] = ["Appearance", "外観", "Aussehen", "Apparence"],
        ["Debug"] = ["Log", "ログ", "Protokoll", "Journal"],
        ["About"] = ["Info", "情報", "Info", "Infos"],
        ["Language"] = ["Language", "言語", "Sprache", "Langue"],
        ["Show A rank"] = ["Show A rank", "Aランクを表示", "A-Ränge anzeigen", "Afficher les rangs A"],
        ["Show S rank"] = ["Show S rank", "Sランクを表示", "S-Ränge anzeigen", "Afficher les rangs S"],
        ["Show HP bar"] = ["Show HP bar", "HPバーを表示", "LP-Balken anzeigen", "Afficher la barre de PV"],
        ["Show HP percent"] = ["Show HP percent", "HP割合を表示", "LP-Prozent anzeigen", "Afficher le pourcentage de PV"],
        ["Show distance"] = ["Show distance", "距離を表示", "Entfernung anzeigen", "Afficher la distance"],
        ["Show time to kill"] = ["Show time to kill", "討伐までの予測時間を表示", "Geschätzte Restkampfzeit anzeigen", "Afficher le temps de combat estimé"],
        ["Show ObjectIndex"] = ["Show ObjectIndex", "ObjectIndexを表示", "ObjectIndex anzeigen", "Afficher ObjectIndex"],
        ["Max display distance"] = ["Max display distance", "最大表示距離", "Maximale Anzeigeentfernung", "Distance maximale d'affichage"],
        ["Time to kill sample window"] = ["Time to kill sample window", "討伐予測の計測期間", "Messzeitraum für die Restkampfzeit", "Période de mesure du temps de combat"],
        ["Play FFXIV sound on detection"] = ["Play FFXIV sound on detection", "検出時にFFXIVの効果音を再生", "FFXIV-Sound bei Erkennung abspielen", "Jouer un son FFXIV à la détection"],
        ["Place map flag on detection"] = ["Place map flag on detection", "検出時にマップへ旗を設置", "Bei Erkennung Kartenflagge setzen", "Placer un drapeau à la détection"],
        ["Create Party Finder from plate"] = ["Create Party Finder from right-clicked plate", "プレート右クリックでパーティ募集を作成", "Partyfinder per Rechtsklick auf Platte erstellen", "Créer une recherche d'équipe par clic droit"],
        ["Sound Effect Id"] = ["Sound effect ID", "効果音ID", "Soundeffekt-ID", "ID de l'effet sonore"],
        ["Sound Repeat Count"] = ["Sound repeat count", "効果音の再生回数", "Soundwiederholungen", "Nombre de répétitions du son"],
        ["Sound Repeat Interval"] = ["Sound repeat interval", "効果音の再生間隔", "Abstand der Soundwiederholungen", "Intervalle entre les sons"],
        ["Speak TTS on detection"] = ["Speak TTS on detection", "検出時に音声で読み上げ", "Sprachausgabe bei Erkennung", "Annonce vocale à la détection"],
        ["Print chat notification"] = ["Print chat notification", "チャットに通知を表示", "Benachrichtigung im Chat", "Afficher une notification dans le chat"],
        ["Notification cooldown"] = ["Notification cooldown", "再通知までの待ち時間", "Sperrzeit zwischen Meldungen", "Délai entre les notifications"],
        ["TTS Volume"] = ["TTS volume", "読み上げ音量", "Lautstärke der Sprachausgabe", "Volume de la synthèse vocale"],
        ["TTS Rate"] = ["TTS rate", "読み上げ速度", "Sprechgeschwindigkeit", "Vitesse de la synthèse vocale"],
        ["TTS Format"] = ["TTS format", "読み上げ文", "Text der Sprachausgabe", "Texte de l'annonce vocale"],
        ["A rank notification distance"] = ["A rank notification distance", "Aランクの通知距離", "Meldeentfernung für A-Ränge", "Distance de notification des rangs A"],
        ["S rank notification distance"] = ["S rank notification distance", "Sランクの通知距離", "Meldeentfernung für S-Ränge", "Distance de notification des rangs S"],
        ["Show announced start time"] = ["Show announced start time", "告知された開始時刻を表示", "Angekündigte Startzeit anzeigen", "Afficher l'heure de départ annoncée"],
        ["Show ET at announcement"] = ["Show ET at announcement", "告知受信時のETを表示", "ET beim Empfang anzeigen", "Afficher l'ET à la réception"],
        ["Show in-progress label"] = ["Show in-progress label", "討伐中のラベルを表示", "Kampfstatus anzeigen", "Afficher le statut du combat"],
        ["Show countdown frame"] = ["Show countdown frame", "カウントダウンの外周枠を表示", "Countdown-Rahmen anzeigen", "Afficher le cadre du compte à rebours"],
        ["Start countdown before announced time"] = ["Start countdown before announced time", "開始時刻の前にカウントダウンを起動", "Countdown vor der Startzeit auslösen", "Lancer le compte à rebours avant le départ"],
        ["ET announcement window"] = ["ET announcement window", "開始ETの前後の許容範囲", "Zeitfenster für ET-Ankündigungen", "Marge autour de l'ET annoncée"],
        ["Start time display after pull"] = ["Start time display after pull", "開始後の表示時間", "Anzeigedauer nach Kampfbeginn", "Durée d'affichage après le départ"],
        ["Countdown seconds"] = ["Countdown seconds", "カウントダウン秒数", "Countdown-Dauer", "Durée du compte à rebours"],
        ["Countdown frame thickness"] = ["Countdown frame thickness", "外周フレームの太さ", "Stärke des Countdown-Rahmens", "Épaisseur du cadre du compte à rebours"],
        ["Scale"] = ["Scale", "全体の倍率", "Skalierung", "Échelle"],
        ["Y Offset"] = ["Y offset", "頭上位置の高さ調整", "Höhenversatz", "Décalage vertical"],
        ["Name Font Size"] = ["Name font size", "名前の文字サイズ", "Schriftgröße des Namens", "Taille du texte du nom"],
        ["HP Bar Width"] = ["HP bar width", "HPバーの幅", "Breite des LP-Balkens", "Largeur de la barre de PV"],
        ["HP Bar Height"] = ["HP bar height", "HPバーの高さ", "Höhe des LP-Balkens", "Hauteur de la barre de PV"],
        ["A rank text color"] = ["A rank text color", "Aランクの文字色", "Textfarbe für A-Ränge", "Couleur du texte des rangs A"],
        ["S rank text color"] = ["S rank text color", "Sランクの文字色", "Textfarbe für S-Ränge", "Couleur du texte des rangs S"],
        ["HP bar color"] = ["HP bar color", "HPバーの色", "Farbe des LP-Balkens", "Couleur de la barre de PV"],
        ["Background color"] = ["Background color", "背景色", "Hintergrundfarbe", "Couleur du fond"],
        ["In-progress background color"] = ["In-progress background color", "討伐中の背景色", "Hintergrundfarbe im Kampf", "Couleur du fond pendant le combat"],
        ["Countdown frame color"] = ["Countdown frame color", "外周フレームの色", "Farbe des Countdown-Rahmens", "Couleur du cadre du compte à rebours"],
        ["Copy debug log"] = ["Copy log", "ログをコピー", "Protokoll kopieren", "Copier le journal"],
        ["Dump debug log"] = ["Save log", "ログを保存", "Protokoll speichern", "Enregistrer le journal"],
        ["Clear debug log"] = ["Clear log", "ログを消去", "Protokoll leeren", "Effacer le journal"],
        ["Game ET"] = ["Game ET", "ゲーム内ET", "Spiel-ET", "ET du jeu"],
        ["waiting"] = ["waiting", "待機中", "Warten", "en attente"],
        ["unavailable"] = ["unavailable", "取得不可", "nicht verfügbar", "indisponible"],
        ["Log saved"] = ["Log saved.", "ログを保存しました。", "Protokoll gespeichert.", "Journal enregistré."],
        ["Log save failed"] = ["Could not save log.", "ログを保存できませんでした。", "Protokoll konnte nicht gespeichert werden.", "Impossible d'enregistrer le journal."],
        ["Version"] = ["Version", "バージョン", "Version", "Version"],
        ["Author"] = ["Author", "製作者", "Autor", "Auteur"],
        ["Distance unit"] = ["%.0f yalms", "%.0f ヤルム", "%.0f Yalm", "%.0f yalms"],
        ["Seconds unit"] = ["%.0f sec", "%.0f 秒", "%.0f Sek.", "%.0f s"],
        ["Interval unit"] = ["%.2f sec", "%.2f 秒", "%.2f Sek.", "%.2f s"],
        ["ET minutes unit"] = ["+/- %.0f ET min", "前後 %.0f ET分", "+/- %.0f ET-Min.", "+/- %.0f min ET"],
        ["In progress"] = ["In progress", "討伐中", "Im Kampf", "Combat en cours"],
        ["ETA"] = ["ETA", "討伐予測", "Restzeit", "Temps estimé"],
        ["Announcement countdown"] = ["ET {0} -> {1}  in {2}s", "ET {0} -> {1}  あと{2}秒", "ET {0} -> {1}  in {2}s", "ET {0} -> {1}  dans {2}s"],
        ["Start countdown"] = ["Start ET {0}  in {1}s", "開始ET {0}  あと{1}秒", "Start ET {0}  in {1}s", "Départ ET {0}  dans {1}s"],
        ["Hunt detected"] = ["[{0}] {1} is in detection range.", "[{0}] {1}が通知範囲内に入りました。", "[{0}] {1} ist in Reichweite.", "[{0}] {1} est à portée de détection."],
        ["Party finder menu"] = ["Party Finder", "パーティ募集", "Partyfinder", "Recherche d'équipe"],
        ["Create hunt recruitment"] = ["Create hunt recruitment", "モブハント募集を作成", "Jagdgruppe erstellen", "Créer une annonce de chasse"],
        ["Recruitment preview"] = ["Recruitment preview", "募集内容の確認", "Vorschau der Gruppensuche", "Aperçu de l'annonce"],
        ["Apply recruitment"] = ["Open recruitment settings", "ゲームの募集設定を開く", "Rekrutierungseinstellungen öffnen", "Ouvrir les paramètres de recrutement"],
        ["Recruitment start"] = ["Start", "開始", "Start", "Début"],
        ["Recruitment already open"] = ["Close the current recruitment editor, then right-click the hunt plate again.", "開いている募集編集画面を閉じてから、プレートをもう一度右クリックしてください。", "Schließe den offenen Rekrutierungseditor und klicke erneut mit rechts auf die Jagdplatte.", "Fermez l'éditeur de recrutement, puis faites à nouveau un clic droit sur la plaque."],
        ["Recruitment already active"] = ["You already have an active recruitment listing.", "すでにパーティ募集中です。", "Du hast bereits eine aktive Gruppensuche.", "Une annonce de recrutement est déjà active."],
        ["Recruitment failed"] = ["Could not open recruitment settings. Please try again.", "募集設定を開けませんでした。もう一度お試しください。", "Rekrutierungseinstellungen konnten nicht geöffnet werden. Bitte erneut versuchen.", "Impossible d'ouvrir les paramètres de recrutement. Veuillez réessayer."],
        ["Cancel"] = ["Cancel", "キャンセル", "Abbrechen", "Annuler"],
        ["Recruitment copied"] = ["Recruitment comment copied to clipboard.", "募集文をクリップボードにコピーしました。", "Kommentar in die Zwischenablage kopiert.", "Commentaire copié dans le presse-papiers."],
        ["Recruitment native note"] = ["The Party Finder window was opened. Check role restrictions before recruiting.", "パーティ募集ウィンドウを開きました。募集開始前にロール制限を確認してください。", "Das Partyfinder-Fenster wurde geöffnet. Bitte Rollenbeschränkungen vor dem Rekrutieren prüfen.", "La fenêtre de recherche d'équipe est ouverte. Vérifiez les restrictions de rôle avant de recruter."],
    };

    static UiText()
    {
        foreach (var entry in Translations)
        {
            if (entry.Value.Length != 4)
                throw new InvalidOperationException($"Expected four translations for {entry.Key}.");
            foreach (var value in entry.Value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException($"Empty translation for {entry.Key}.");
            }
        }
    }

    public static IEnumerable<string> Keys => Translations.Keys;

    public static string Get(string key, UiLanguage language)
    {
        if (!Translations.TryGetValue(key, out var values))
            return key;
        var index = (int)language;
        return values[index >= 0 && index < values.Length ? index : 0];
    }

    public static string Label(string key, UiLanguage language) => $"{Get(key, language)}###{key}";

    public static string Format(string key, UiLanguage language, params object[] values)
        => string.Format(CultureInfo.InvariantCulture, Get(key, language), values);
}
