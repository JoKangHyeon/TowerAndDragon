using System;
using CsvHelper.Configuration.Attributes;
using UnityEngine;

public sealed class PopulationAllocation
{
    public PopulationAssignmentType AssignmentType {get;}
    public int Capacity {get;}
    public int AssignedPopulation {get; private set;}

    public int AvailableCap => 
        Capacity - AssignedPopulation;
    public float StaffingRatio => 
        Capacity == 0 ? 0f : (float)AssignedPopulation / Capacity; 

    internal PopulationAllocation(
        PopulationAssignmentType assignmentType,
        int capacity)

    {
        AssignmentType = assignmentType;
        Capacity = capacity;
    }

    internal void Assign (int amount)
    {
        AssignedPopulation += amount;
    }

    internal void Unassign(int amount)
    {
        AssignedPopulation -= amount;
    }

}