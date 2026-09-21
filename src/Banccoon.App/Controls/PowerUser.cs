namespace Banccoon.App.Controls;

public static class PowerUser
{
    public static readonly BindableProperty IsPowerUserElementProperty =
        BindableProperty.CreateAttached(
            "IsPowerUserElement",
            typeof(bool),
            typeof(PowerUser),
            false,
            propertyChanged: UpdateVisibility);

    public static readonly BindableProperty ShowPowerUserElementsProperty =
        BindableProperty.CreateAttached(
            "ShowPowerUserElements",
            typeof(bool),
            typeof(PowerUser),
            false,
            propertyChanged: UpdateVisibility);

    public static bool GetIsPowerUserElement(BindableObject view)
    {
        return (bool)view.GetValue(IsPowerUserElementProperty);
    }

    public static void SetIsPowerUserElement(BindableObject view, bool value)
    {
        view.SetValue(IsPowerUserElementProperty, value);
    }

    public static bool GetShowPowerUserElements(BindableObject view)
    {
        return (bool)view.GetValue(ShowPowerUserElementsProperty);
    }

    public static void SetShowPowerUserElements(BindableObject view, bool value)
    {
        view.SetValue(ShowPowerUserElementsProperty, value);
    }

    private static void UpdateVisibility(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement visualElement)
        {
            return;
        }

        var isPowerUserElement = GetIsPowerUserElement(visualElement);
        if (!isPowerUserElement)
        {
            return;
        }

        visualElement.IsVisible = GetShowPowerUserElements(visualElement);
    }
}
