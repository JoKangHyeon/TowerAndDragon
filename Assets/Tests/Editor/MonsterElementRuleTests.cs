using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MonsterElementRuleTests
{
    private MonsterData _monsterData;

    [SetUp]
    public void SetUp()
    {
        _monsterData = ScriptableObject.CreateInstance<MonsterData>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_monsterData);
    }

    [Test]
    public void Normal_AcceptsElementalAndNonElementalAttacks()
    {
        Configure(MonsterElementRule.Normal, DragonType.Fire);

        Assert.That(_monsterData.AcceptsElement(null), Is.True);
        Assert.That(_monsterData.AcceptsElement(DragonType.Fire), Is.True);
        Assert.That(_monsterData.AcceptsElement(DragonType.Ice), Is.True);
    }

    [Test]
    public void OnlyMatchingElement_AcceptsOnlyConfiguredElement()
    {
        Configure(MonsterElementRule.OnlyMatchingElement, DragonType.Fire);

        Assert.That(_monsterData.AcceptsElement(null), Is.False);
        Assert.That(_monsterData.AcceptsElement(DragonType.Fire), Is.True);
        Assert.That(_monsterData.AcceptsElement(DragonType.Ice), Is.False);
    }

    [Test]
    public void ImmuneToMatchingElement_RejectsOnlyConfiguredElement()
    {
        Configure(MonsterElementRule.ImmuneToMatchingElement, DragonType.Fire);

        Assert.That(_monsterData.AcceptsElement(null), Is.True);
        Assert.That(_monsterData.AcceptsElement(DragonType.Fire), Is.False);
        Assert.That(_monsterData.AcceptsElement(DragonType.Ice), Is.True);
    }

    private void Configure(MonsterElementRule rule, DragonType element)
    {
        SerializedObject serializedData = new SerializedObject(_monsterData);
        serializedData.FindProperty("_elementRule").enumValueIndex = (int)rule;
        serializedData.FindProperty("_element").enumValueIndex = (int)element;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
    }
}
