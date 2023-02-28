using System;
// to prevent namespace conflicts
using ARRMaterial = Microsoft.Azure.RemoteRendering.Material;

public class OverrideMaterialProperty<T>
{
    public OverrideMaterialProperty(T originalValue, ARRMaterial material, Action<ARRMaterial, T> applyAction)
    {
        OriginalValue = originalValue;
        overrideValue = originalValue;
        targetMaterial = material;

        ActiveChanged = null;
        overrideActive = true;
        ApplyValue = applyAction;
    }

    public event Action ActiveChanged;

    private ARRMaterial targetMaterial;
    private bool overrideActive;
    public bool OverrideActive
    {
        get => overrideActive;
        set
        {
            if (overrideActive != value)
            {
                overrideActive = value;
                ActiveChanged?.Invoke();
                if (overrideActive)
                {
                    ApplyValue(targetMaterial, OverrideValue);
                }
                else
                {
                    ApplyValue(targetMaterial, OriginalValue);
                }
            }
        }
    }

    public T OriginalValue { get; }

    private T overrideValue;
    public T OverrideValue
    {
        get => overrideValue;
        set
        {
            overrideValue = value;
            if (OverrideActive)
                ApplyValue(targetMaterial, overrideValue);
        }
    }

    private Action<ARRMaterial, T> ApplyValue;
}