﻿using UnityEditor;
using UnityEngine;
using AlaslTools;

namespace AutoLevel
{
    public enum BlockEditMode
    {
        None,
        Fill,
        Connection,
        Layer
    }

    public class RepoEntitiesEditorSettingsSO : BaseSO<RepoEntitiesEditorSettings>
    {
        public RepoEntitiesEditorSettings.Settings settings;

        public RepoEntitiesEditorSettingsSO(Object target) : base(target) { }

        public RepoEntitiesEditorSettingsSO(SerializedObject serializedObject) : base(serializedObject) { }
    }

    public class RepoEntitiesEditorSettings : ScriptableObject
    {
        public static RepoEntitiesEditorSettings GetSettings()
        {
            var scriptDir = System.IO.Path.Combine(EditorHelper.GetAssemblyDirectory<RepoEntitiesEditorSettings>(),"Scripts" ,"Resources");
            var path = System.IO.Path.Combine(scriptDir, "Repo Entities Settings.asset");
            var settings = AssetDatabase.LoadAssetAtPath<RepoEntitiesEditorSettings>(path);
            if (settings == null)
            {
                settings = CreateInstance<RepoEntitiesEditorSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            return settings;
        }

        [System.Serializable]
        public class Settings
        {
            public bool             DrawSelfConnections;
            public int              MaxConnectionsDrawCount;
            [Space]
            public KeyCode banConnectionKey         = KeyCode.Alpha1;
            public KeyCode exclusiveConnectionKey   = KeyCode.Alpha3;

            [HideInInspector]
            public BlockEditMode    EditMode;
            [HideInInspector]
            public bool ShowedConnectionTutorial;
        }

        [SerializeField]
        public Settings settings =
            new Settings()
            {
                DrawSelfConnections = true,
                MaxConnectionsDrawCount = 30,
                ShowedConnectionTutorial = false,
            };
    }

}