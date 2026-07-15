using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    [FormerlySerializedAs("CurrentRun")]
    private RunData _currentRun;

    [SerializeField]
    [FormerlySerializedAs("CycleManager")]
    private CycleManager _cycleManager;

    [SerializeField]
    [FormerlySerializedAs("DefaultLights")]
    private List<CycleLight> _defaultLights;

    public RunData CurrentRun => _currentRun;
    public CycleManager CycleManager => _cycleManager;
    public List<CycleLight> DefaultLights => _defaultLights;

    private void Awake()
    {
        _cycleManager.Construct(this);
    }

    private void Start()
    {
        foreach (CycleLight light in _defaultLights)
        {
            light.Construct(_cycleManager);
        }
    }
}
