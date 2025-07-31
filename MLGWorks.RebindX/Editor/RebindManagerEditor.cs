using System.IO;
using UnityEditor;
using UnityEngine;

namespace MLGWorks.RebindX.Runtime.Editors
{
    [CustomEditor(typeof(RebindManager))]
    public class RebindManagerEditor : Editor
    {
        // Serialized props for every public field on RebindManager
        private SerializedProperty pathTypeProp;
        private SerializedProperty relativePathProp;
        private SerializedProperty customPathProp;
        private SerializedProperty customFileName;

        private void OnEnable()
        {
            pathTypeProp = serializedObject.FindProperty("pathType");
            relativePathProp = serializedObject.FindProperty("relativePath");
            customPathProp = serializedObject.FindProperty("customPath");
            customFileName = serializedObject.FindProperty("fileName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Rebind Manager Configuration", EditorStyles.boldLabel);

            // Path settings
            EditorGUILayout.PropertyField(pathTypeProp, new GUIContent("Rebind Location"));
            if ((FileLocationType)pathTypeProp.enumValueIndex != FileLocationType.Custom)
            {
                EditorGUILayout.PropertyField(relativePathProp, new GUIContent("Relative Path"));
            }
            else
            {
                EditorGUILayout.PropertyField(customPathProp, new GUIContent("Custom Path"));
            }
            EditorGUILayout.PropertyField(customFileName, new GUIContent("File Name"));

            EditorGUILayout.Space();

            // Open folder button
            if (GUILayout.Button("Open Rebind Folder"))
            {
                var rebindManager = (RebindManager)target;
                string dirPath = rebindManager.DirectoryPath;
                string filePath = rebindManager.FilePath;
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                EditorUtility.RevealInFinder(filePath);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
