using UnityEngine;
using System;

public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    public LocationZone CurrentZone { get; private set; }

    public event Action<LocationZone> OnLocationChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public string CurrentLocationName
    {
        get { return CurrentZone != null ? CurrentZone.DisplayName : string.Empty; }
    }

    public void EnterZone(LocationZone zone)
    {
        if (zone == CurrentZone) return;
        CurrentZone = zone;
        OnLocationChanged?.Invoke(zone);
    }

    public void ExitZone(LocationZone zone)
    {
        if (zone != CurrentZone) return;
        CurrentZone = null;
        OnLocationChanged?.Invoke(null);
    }
}