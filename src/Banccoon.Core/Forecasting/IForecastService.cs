namespace Banccoon.Core.Forecasting;

public interface IForecastService
{
    ForecastResult CreateForecast(ForecastRequest request);
}
