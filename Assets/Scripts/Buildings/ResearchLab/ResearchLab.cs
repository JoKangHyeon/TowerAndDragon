using UnityEngine;

public sealed class ResearchLab : Building
{
    [SerializeField] private ResearchLabData _data;

    public ResearchLabData Data => _data;
    public ResearchLabPopulation Population => GetComponent<ResearchLabPopulation>();
}
