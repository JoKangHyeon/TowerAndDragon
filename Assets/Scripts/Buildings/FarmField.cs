using UnityEngine;

public class FarmField : Building
{
    [SerializeField] private int _currentCap;
    [SerializeField] private int _maxCap;
    

    public bool IsFull => _currentCap == _maxCap;
    
}