using System;
using UnityEngine;



[Flags]
public enum MonsterTargetType
{
    None = 0,
    Tower = 1 << 0,
    Dragon = 1 << 1
}
