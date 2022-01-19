﻿using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Internal;

namespace LDtkUnity.Editor
{
    internal abstract class LDtkJsonFileEditor<T> : LDtkEditor
    {
        protected LDtkTreeViewWrapper Tree;
        protected T JsonData = default;

        public void OnDisable()
        {
            Tree?.Dispose();
        }
        
        public override void OnInspectorGUI()
        {
            TryCacheJson();

            if (JsonData == null)
            {
                Assert.AreNotEqual(JsonData, default);
                return;
            }

            using (new LDtkGUIScope(true))
            {
                DrawInspectorGUI();
                LDtkEditorGUIUtility.DrawDivider();
                Tree?.OnGUI();
            }
        }

        protected void TryCacheJson()
        {
            LDtkJsonFile<T> file = (LDtkJsonFile<T>)target;
            Assert.IsNotNull(file);

            if (JsonData == null)
            {
                JsonData = file.FromJson;
            }
        }

        protected abstract void DrawInspectorGUI();
        
        protected void DrawCountOfItems(int? count, string single, string plural, Texture2D icon = null)
        {
            if (count == null)
            {
                DrawText("(Error)", icon);
                return;
            }
            
            string naming = count == 1 ? single : plural;
            DrawText($"{count} {naming}", icon);
        }
        
        protected void DrawText(string text, Texture2D icon = null)
        {
            GUIContent content = new GUIContent()
            {
                text = text,
                image = icon
            };
            EditorGUILayout.LabelField(content);
        }
    }
}