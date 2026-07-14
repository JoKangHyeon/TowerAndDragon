using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class CycleLight : MonoBehaviour
{
    /// <summary>
    /// false면 밤에 빛을 킴
    /// </summary>
    public bool IsOnWhileDay;

    private Light2D _light2D;
    private CycleManager _cycleManager;

    private void Awake()
    {
        _light2D = GetComponent<Light2D>();
    }

    public void Construct(CycleManager cycleManager)
    {
        _cycleManager = cycleManager;
        _cycleManager.OnDayStart.AddListener(OnDayStart);
        _cycleManager.OnNightStart.AddListener(OnNightStart);
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(OnDayStart);
            _cycleManager.OnNightStart.RemoveListener(OnNightStart);
        }
    }

    public void OnDayStart(int cycle)
    {
        if (IsOnWhileDay)
        {
            _light2D.enabled = true;
        }
        else
        {
            _light2D.enabled = false;
        }
    }

    public void OnNightStart(int cycle)
    {
        if (IsOnWhileDay)
        {
            _light2D.enabled = false;
        }
        else
        {
            _light2D.enabled = true;
        }
    }
}
