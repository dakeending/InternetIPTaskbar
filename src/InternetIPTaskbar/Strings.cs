using System.Globalization;

namespace InternetIPTaskbar;

/// <summary>
/// All user-visible text. English is the default; Russian is used when the Windows UI language is Russian.
/// To add a language, extend <see cref="Pick"/> and add the translation next to each English string.
/// </summary>
internal static class Strings
{
    static readonly bool Russian =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase);

    static string Pick(string en, string ru) => Russian ? ru : en;

    // ----- status -----
    public static string OverlayOffline => Pick("no network", "нет сети");
    public static string Offline => Pick("No network", "Нет сети");
    public static string Detecting => Pick("Detecting IP…", "Определяю IP…");
    public static string UpdateFailedTip => Pick("(update failed)", "(не удалось обновить)");
    public static string UpdateFailedMenu => Pick("The last update failed", "Последнее обновление не удалось");
    public static string Ipv6MismatchMenu => Pick("⚠ IPv6 from a different country — possible VPN leak", "⚠ IPv6 в другой стране — возможна утечка VPN");

    // ----- balloons -----
    public static string IpCopied => Pick("IP copied", "IP скопирован");
    public static string Ipv6Copied => Pick("IPv6 copied", "IPv6 скопирован");
    public static string AlertTitle => Pick("The VPN may be off", "Возможно, VPN отключён");

    public static string AlertBody(bool byIp, string? ip, string? country) => Pick(
        $"You are going online with your real {(byIp ? "IP" : "region")}: {ip} ({country ?? "?"})",
        $"Выход в интернет под вашим реальным {(byIp ? "IP" : "регионом")}: {ip} ({country ?? "?"})");

    public static string Ipv6LeakTitle => Pick("Possible IPv6 leak", "Возможна утечка IPv6");

    public static string Ipv6LeakBody(string? v4Country, string? v6Country) => Pick(
        $"IPv4: {v4Country}, IPv6: {v6Country}. IPv6 traffic bypasses the VPN.",
        $"IPv4: {v4Country}, IPv6: {v6Country}. Трафик по IPv6 идёт мимо VPN.");

    public static string HomeNotSetTitle => Pick("Real data is not set", "Реальные данные не заданы");
    public static string HomeNotSetBody => Pick(
        "Turn the VPN off and choose “Remember current IP and country as real”.",
        "Отключите VPN и выберите «Запомнить текущие IP и страну как реальные».");
    public static string HomeSavedTitle => Pick("Real data saved", "Реальные данные сохранены");

    public static string HomeSavedBody(string? country) => Pick(
        $"Country: {country ?? "?"}. The alert will fire when you go online with this IP.",
        $"Страна: {country ?? "?"}. Тревога включится при выходе под этим IP.");

    public static string UninstallTitle => Pick("Uninstall", "Удаление");
    public static string UninstallFailed(string message) => Pick($"Could not start the uninstaller: {message}", $"Не удалось запустить деинсталлятор: {message}");
    public static string AutostartTitle => Pick("Autostart", "Автозапуск");
    public static string AutostartFailed(string message) => Pick($"Could not change autostart: {message}", $"Не удалось изменить автозапуск: {message}");

    // ----- menu -----
    public static string CopyIp => Pick("Copy IP", "Копировать IP");
    public static string CopyIpv6 => Pick("Copy IPv6", "Копировать IPv6");
    public static string CheckNow => Pick("Check now", "Проверить сейчас");
    public static string Settings => Pick("Settings", "Настройки");
    public static string Interval => Pick("Check interval", "Интервал проверки");
    public static string Seconds(int n) => Pick($"{n} s", $"{n} сек");
    public static string Placement => Pick("Position", "Положение");
    public static string PlacementNearTray => Pick("Next to the notification area", "Рядом с областью уведомлений");
    public static string PlacementCenter => Pick("Centered on the taskbar", "По центру панели");
    public static string VpnGuard => Pick("VPN guard", "Контроль VPN");
    public static string GuardOff => Pick("Off", "Выключен");
    public static string GuardByIp => Pick("Alert when I go online with my real IP", "Тревога при выходе под моим реальным IP");
    public static string GuardByCountry => Pick("Alert when I go online from my real country", "Тревога при выходе из моей реальной страны");
    public static string HomeIpLine(string? ip) => Pick($"Real IP: {ip ?? "not set"}", $"Реальный IP: {ip ?? "не задан"}");
    public static string HomeCountryLine(string? c) => Pick($"Real country: {c ?? "not set"}", $"Реальная страна: {c ?? "не задана"}");
    public static string RememberHome => Pick("Remember current IP and country as real (turn the VPN off first)", "Запомнить текущие IP и страну как реальные (отключите VPN)");
    public static string ForgetHome => Pick("Forget the real IP and country", "Забыть реальные IP и страну");
    public static string Horn => Pick("Horn on VPN alert", "Гудок при тревоге VPN");
    public static string CheckIpv6 => Pick("Check IPv6", "Проверять IPv6");
    public static string StartWithWindows => Pick("Start with Windows", "Запускать при входе в Windows");
    public static string VerboseLog => Pick("Verbose log", "Подробный лог");
    public static string UninstallItem => Pick("Uninstall…", "Удалить программу…");
    public static string OpenLog => Pick("Open log", "Открыть лог");
    public static string Exit(string version) => Pick($"Exit (v{version})", $"Выход (v{version})");
}
