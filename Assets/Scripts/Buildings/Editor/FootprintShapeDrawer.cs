using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FootprintShape))]
public class FootprintShapeDrawer : PropertyDrawer
{
    private const int MIN_SIZE = 1;
    private const int MAX_SIZE = 10;
    private const float CELL_SIZE = 20f;
    private const float CELL_SPACING = 2f;
    private static readonly Color OCCUPIED_COLOR = new Color(0.6f, 1f, 0.6f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty widthProp = property.FindPropertyRelative("_width");
        SerializedProperty heightProp = property.FindPropertyRelative("_height");
        SerializedProperty cellsProp = property.FindPropertyRelative("_cells");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect labelRect = new Rect(position.x, position.y, position.width, lineHeight);
        EditorGUI.LabelField(labelRect, label);

        Rect sizeRect = new Rect(position.x, labelRect.yMax + spacing, position.width, lineHeight);
        DrawSizeFields(sizeRect, widthProp, heightProp, cellsProp);

        Rect gridRect = new Rect(position.x, sizeRect.yMax + spacing, position.width, GetGridHeight(heightProp.intValue));
        DrawGrid(gridRect, widthProp.intValue, heightProp.intValue, cellsProp);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty heightProp = property.FindPropertyRelative("_height");
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        return lineHeight + spacing + lineHeight + spacing + GetGridHeight(heightProp.intValue);
    }

    private static float GetGridHeight(int height) => height * (CELL_SIZE + CELL_SPACING);

    private void DrawSizeFields(Rect rect, SerializedProperty widthProp, SerializedProperty heightProp, SerializedProperty cellsProp)
    {
        float halfWidth = rect.width / 2f;
        Rect widthRect = new Rect(rect.x, rect.y, halfWidth - CELL_SPACING, rect.height);
        Rect heightRect = new Rect(rect.x + halfWidth + CELL_SPACING, rect.y, halfWidth - CELL_SPACING, rect.height);

        int oldWidth = widthProp.intValue;
        int oldHeight = heightProp.intValue;
        int newWidth = Mathf.Clamp(EditorGUI.IntField(widthRect, "Width", oldWidth), MIN_SIZE, MAX_SIZE);
        int newHeight = Mathf.Clamp(EditorGUI.IntField(heightRect, "Height", oldHeight), MIN_SIZE, MAX_SIZE);

        if (newWidth != oldWidth || newHeight != oldHeight)
        {
            ResizeCells(cellsProp, oldWidth, oldHeight, newWidth, newHeight);
            widthProp.intValue = newWidth;
            heightProp.intValue = newHeight;
        }
    }

    private void ResizeCells(SerializedProperty cellsProp, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        bool[] oldCells = new bool[cellsProp.arraySize];
        for (int i = 0; i < oldCells.Length; i++)
            oldCells[i] = cellsProp.GetArrayElementAtIndex(i).boolValue;

        cellsProp.arraySize = newWidth * newHeight;
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                bool isWithinOldBounds = x < oldWidth && y < oldHeight;
                bool occupied = isWithinOldBounds ? oldCells[y * oldWidth + x] : true;
                cellsProp.GetArrayElementAtIndex(y * newWidth + x).boolValue = occupied;
            }
        }
    }

    private void DrawGrid(Rect rect, int width, int height, SerializedProperty cellsProp)
    {
        if (cellsProp.arraySize != width * height)
            return;

        for (int y = 0; y < height; y++)
        {
            int displayRow = height - 1 - y;
            for (int x = 0; x < width; x++)
            {
                SerializedProperty cellProp = cellsProp.GetArrayElementAtIndex(y * width + x);
                Rect cellRect = new Rect(
                    rect.x + x * (CELL_SIZE + CELL_SPACING),
                    rect.y + displayRow * (CELL_SIZE + CELL_SPACING),
                    CELL_SIZE,
                    CELL_SIZE);

                Color previousColor = GUI.color;
                GUI.color = cellProp.boolValue ? OCCUPIED_COLOR : previousColor;
                bool newValue = GUI.Toggle(cellRect, cellProp.boolValue, GUIContent.none, EditorStyles.miniButton);
                GUI.color = previousColor;

                if (newValue != cellProp.boolValue)
                    cellProp.boolValue = newValue;
            }
        }
    }
}
