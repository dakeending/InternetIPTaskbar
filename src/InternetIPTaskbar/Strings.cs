using System.Globalization;

namespace InternetIPTaskbar;

/// <summary>
/// All user-visible text in three languages: English (the default and the fallback), Russian and
/// Traditional Chinese as written in Hong Kong. The language follows the Windows display language
/// unless the user picks one (<see cref="Language"/>). To add a language, extend <see cref="Pick"/>
/// and <see cref="Lang"/> and translate every string below.
/// </summary>
internal static class Strings
{
    enum Lang { En, Ru, Zh }

    /// <summary>The language chosen in the menu. <see cref="UiLanguage.Auto"/> follows Windows.</summary>
    public static UiLanguage Language { get; set; } = UiLanguage.Auto;

    static Lang Current => Language switch
    {
        UiLanguage.English => Lang.En,
        UiLanguage.Russian => Lang.Ru,
        UiLanguage.ChineseHK => Lang.Zh,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "ru" => Lang.Ru,
            "zh" => Lang.Zh,
            _ => Lang.En
        }
    };

    static string Pick(string en, string ru, string zh) => Current switch
    {
        Lang.Ru => ru,
        Lang.Zh => zh,
        _ => en
    };

    // ----- status -----
    public static string OverlayOffline => Pick("no network", "нет сети", "沒有網絡");
    public static string Offline => Pick("No network", "Нет сети", "沒有網絡連線");
    public static string Detecting => Pick("Detecting IP…", "Определяю IP…", "正在偵測 IP…");
    public static string UpdateFailedTip => Pick("(update failed)", "(не удалось обновить)", "（更新失敗）");
    public static string UpdateFailedMenu => Pick("The last update failed", "Последнее обновление не удалось", "上次更新失敗");
    public static string Ipv6MismatchMenu => Pick(
        "⚠ IPv6 from a different country — possible VPN leak",
        "⚠ IPv6 в другой стране — возможна утечка VPN",
        "⚠ IPv6 來自另一個國家或地區 — VPN 可能洩漏");

    // ----- balloons -----
    public static string IpCopied => Pick("IP copied", "IP скопирован", "已複製 IP");
    public static string Ipv6Copied => Pick("IPv6 copied", "IPv6 скопирован", "已複製 IPv6");
    public static string AlertTitle => Pick("The VPN may be off", "Возможно, VPN отключён", "VPN 可能已關閉");

    public static string AlertBody(AlertMode mode, string? ip, string? country)
    {
        string c = country ?? "?";
        return mode switch
        {
            AlertMode.ByCountry => Pick(
                $"You are going online from your real country: {ip} ({c})",
                $"Выход в интернет из вашей реальной страны: {ip} ({c})",
                $"你正從真實所在的國家或地區上網：{ip}（{c}）"),
            AlertMode.ByRange => Pick(
                $"You are going online from your real IP range: {ip} ({c})",
                $"Выход в интернет из вашего реального диапазона IP: {ip} ({c})",
                $"你正使用真實 IP 範圍上網：{ip}（{c}）"),
            _ => Pick(
                $"You are going online with your real IP: {ip} ({c})",
                $"Выход в интернет под вашим реальным IP: {ip} ({c})",
                $"你正使用真實 IP 上網：{ip}（{c}）")
        };
    }

    public static string Ipv6LeakTitle => Pick("Possible IPv6 leak", "Возможна утечка IPv6", "IPv6 可能洩漏");

    public static string Ipv6LeakBody(string? v4Country, string? v6Country) => Pick(
        $"IPv4: {v4Country}, IPv6: {v6Country}. IPv6 traffic bypasses the VPN.",
        $"IPv4: {v4Country}, IPv6: {v6Country}. Трафик по IPv6 идёт мимо VPN.",
        $"IPv4：{v4Country}，IPv6：{v6Country}。IPv6 流量沒有經過 VPN。");

    public static string HomeNotSetTitle => Pick("Real data is not set", "Реальные данные не заданы", "尚未設定真實資料");
    public static string HomeNotSetBody => Pick(
        "Turn the VPN off and use one of the “Remember current …” items.",
        "Отключите VPN и выберите один из пунктов «Запомнить текущий …».",
        "請先關閉 VPN，然後選擇其中一個「記住目前的…」項目。");
    public static string HomeSavedTitle => Pick("Real data saved", "Реальные данные сохранены", "已儲存真實資料");

    public static string HomeSavedBody(AlertMode mode, string value) => mode switch
    {
        AlertMode.ByRange => Pick(
            $"Real IP range: {value}. Alert when you go online from this range.",
            $"Реальный диапазон IP: {value}. Тревога при выходе из этого диапазона.",
            $"真實 IP 範圍：{value}。從此範圍上網時會發出警報。"),
        AlertMode.ByCountry => Pick(
            $"Real country: {value}. Alert when you go online from this country.",
            $"Реальная страна: {value}. Тревога при выходе из этой страны.",
            $"真實國家或地區：{value}。從此國家或地區上網時會發出警報。"),
        _ => Pick(
            $"Real IP: {value}. Alert when you go online with this IP.",
            $"Реальный IP: {value}. Тревога при выходе под этим IP.",
            $"真實 IP：{value}。使用此 IP 上網時會發出警報。")
    };

    public static string UninstallTitle => Pick("Uninstall", "Удаление", "解除安裝");
    public static string UninstallFailed(string message) => Pick(
        $"Could not start the uninstaller: {message}",
        $"Не удалось запустить деинсталлятор: {message}",
        $"無法啟動解除安裝程式：{message}");
    public static string AutostartTitle => Pick("Autostart", "Автозапуск", "自動啟動");
    public static string AutostartFailed(string message) => Pick(
        $"Could not change autostart: {message}",
        $"Не удалось изменить автозапуск: {message}",
        $"無法更改自動啟動設定：{message}");

    // ----- menu -----
    public static string CopyIp => Pick("Copy IP", "Копировать IP", "複製 IP");
    public static string CopyIpv6 => Pick("Copy IPv6", "Копировать IPv6", "複製 IPv6");
    public static string CheckNow => Pick("Check now", "Проверить сейчас", "立即檢查");
    public static string Settings => Pick("Settings", "Настройки", "設定");
    public static string Interval => Pick("Check interval", "Интервал проверки", "檢查間隔");
    public static string Seconds(int n) => Pick($"{n} s", $"{n} сек", $"{n} 秒");
    public static string Placement => Pick("Position", "Положение", "位置");
    public static string PlacementNearTray => Pick("Next to the notification area", "Рядом с областью уведомлений", "靠近通知區域");
    public static string PlacementCenter => Pick("Centered on the taskbar", "По центру панели", "工作列中央");
    public static string VpnGuard => Pick("VPN guard", "Контроль VPN", "VPN 守護");
    public static string GuardOff => Pick("Off", "Выключен", "關閉");
    // the three alert modes, each with its remembered value
    public static string GuardByIp(string? ip) => Pick(
        $"Alert on my real IP — {ip ?? "not set"}",
        $"Тревога по реальному IP — {ip ?? "не задан"}",
        $"按真實 IP 警報 — {ip ?? "未設定"}");
    public static string GuardByRange(string? range) => Pick(
        $"Alert on my real IP range — {range ?? "not set"}",
        $"Тревога по реальному диапазону IP — {range ?? "не задан"}",
        $"按真實 IP 範圍警報 — {range ?? "未設定"}");
    public static string GuardByCountry(string? country) => Pick(
        $"Alert on my real country — {country ?? "not set"}",
        $"Тревога по реальной стране — {country ?? "не задана"}",
        $"按真實國家或地區警報 — {country ?? "未設定"}");

    public static string RememberIp => Pick(
        "Remember current IP as real (turn the VPN off first)",
        "Запомнить текущий IP как реальный (отключите VPN)",
        "記住目前的 IP 為真實 IP（請先關閉 VPN）");
    public static string RememberRange => Pick(
        "Remember current IP range as real",
        "Запомнить текущий диапазон IP как реальный",
        "記住目前的 IP 範圍為真實範圍");
    public static string RememberCountry => Pick(
        "Remember current country as real",
        "Запомнить текущую страну как реальную",
        "記住目前的國家或地區為真實所在地");
    public static string ForgetHome => Pick("Forget the real data", "Забыть реальные данные", "清除真實資料");
    public static string Horn => Pick("Horn on VPN alert", "Гудок при тревоге VPN", "警報時響起汽笛");
    public static string CheckIpv6 => Pick("Check IPv6", "Проверять IPv6", "檢查 IPv6");
    public static string StartWithWindows => Pick("Start with Windows", "Запускать при входе в Windows", "隨 Windows 啟動");
    public static string VerboseLog => Pick("Verbose log", "Подробный лог", "詳細記錄檔");
    public static string UninstallItem => Pick("Uninstall…", "Удалить программу…", "解除安裝…");
    public static string OpenLog => Pick("Open log", "Открыть лог", "開啟記錄檔");
    public static string Exit(string version) => Pick($"Exit (v{version})", $"Выход (v{version})", $"結束（v{version}）");

    // ----- language menu (language names are shown in their own language) -----
    public static string LanguageMenu => Pick("Language", "Язык", "語言");
    public static string LanguageAuto => Pick("Auto (Windows language)", "Авто (язык Windows)", "自動（跟隨 Windows 語言）");
    public const string LanguageEnglish = "English";
    public const string LanguageRussian = "Русский";
    public const string LanguageChinese = "繁體中文（香港）";
}
