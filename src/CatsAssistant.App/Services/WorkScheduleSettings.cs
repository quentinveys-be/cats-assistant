using System.Globalization;
using CatsAssistant.Store;

namespace CatsAssistant.App.Services;

/// <summary>
/// Durée de travail attendue (issue #22, tâche 2) : défaut 7:36/jour, 38 h/semaine, configurable via
/// <see cref="ISettingsRepository"/>. Partagé entre l'écran Rattrapage et le badge tray pour rester cohérent.
/// À terme alimentable par WorkCalendars SAP (Phase 4, non bloquant).
/// </summary>
public static class WorkScheduleSettings
{
    public const double DefaultExpectedHoursPerDay = 7.6; // 7:36
    public const double DefaultExpectedHoursPerWeek = 38.0;

    private const string ExpectedHoursPerDaySettingKey = "catchup.expectedHoursPerDay";
    private const string ExpectedHoursPerWeekSettingKey = "catchup.expectedHoursPerWeek";

    public static double ExpectedHoursPerDay(ISettingsRepository? settings) =>
        ParseOrDefault(settings?.Get(ExpectedHoursPerDaySettingKey), DefaultExpectedHoursPerDay);

    public static double ExpectedHoursPerWeek(ISettingsRepository? settings) =>
        ParseOrDefault(settings?.Get(ExpectedHoursPerWeekSettingKey), DefaultExpectedHoursPerWeek);

    private static double ParseOrDefault(string? raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
