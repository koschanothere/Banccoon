using Banccoon.Core.Forecasting;

namespace Banccoon.Core.Models;

public sealed record AppSettings(
    string DefaultCurrency,
    ForecastPeriod DefaultForecastPeriod,
    ReminderFrequency ReminderFrequency);
