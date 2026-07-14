using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public RunData CurrentRun;

    public CycleManager CycleManager;


    public List<CycleLight> DefaultLights;

    private void Awake()
    {
        CycleManager.Construct(this);
    }

    private void Start()
    {
        foreach (CycleLight light in DefaultLights)
        {
            light.Construct(CycleManager);
        }
    }
}
