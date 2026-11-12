using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 설정 화면 (Edit → Project Settings → Work Log, 또는 Tools → 작업 기록 → 설정).<br/>
    /// 위쪽은 프로젝트 공용 설정(<c>ProjectSettings/WorkLogSettings.json</c>), 아래쪽은 이 PC만의 설정입니다.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    public class WorkLogSettingsProvider : SettingsProvider
    {
        #region [ 상수 (Constants) ]

        private const string SETTINGS_PATH = "Project/Work Log";
        private const string MENU_PATH = "Tools/작업 기록/설정";

        #endregion

        #region [ 내부 상태 (Internal State) ]

        private WorkLogSettings _settings;
        private bool _isDirty;

        #endregion

        #region [ 생성 (Construction) ]

        private WorkLogSettingsProvider() : base(SETTINGS_PATH, SettingsScope.Project, new HashSet<string> { "작업 기록", "Work Log", "WorkLog" })
        {
        }

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// Project Settings에 이 화면을 등록합니다.
        /// </summary>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new WorkLogSettingsProvider();
        }

        /// <summary>
        /// 설정 화면을 엽니다.
        /// </summary>
        [MenuItem(MENU_PATH, false, 20)]
        public static void Open()
        {
            SettingsService.OpenProjectSettings(SETTINGS_PATH);
        }

        /// <summary>
        /// 화면이 열릴 때 설정 파일을 다시 읽습니다.
        /// </summary>
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = WorkLogSettings.Load();
            _isDirty = false;
        }

        /// <summary>
        /// 설정 화면을 그립니다.
        /// </summary>
        public override void OnGUI(string searchContext)
        {
            if (_settings == null)
                _settings = WorkLogSettings.Load();

            EditorGUIUtility.labelWidth = 190f;
            DrawProjectSettings();
            EditorGUILayout.Space(12f);
            DrawLocalSettings();
        }

        #endregion

        #region [ 화면 그리기 (Drawing) ]

        private void DrawProjectSettings()
        {
            string folderName = WorkLogSettings.ProjectFolderName;
            EditorGUILayout.LabelField("프로젝트 공용 설정 (ProjectSettings/WorkLogSettings.json, Git에 올라감)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            _settings.EntriesDir = EditorGUILayout.TextField(new GUIContent("항목 폴더", $"entryNNN.md를 저장하는 폴더 (프로젝트 폴더 기준). 비우면 {WorkLogSettings.DEFAULT_ENTRIES_DIR}"), _settings.EntriesDir);
            _settings.MdPath = EditorGUILayout.TextField(new GUIContent("MD 경로", $"작업 기록 폴더 기준. 비우면 MD/{folderName}_작업기록.md"), _settings.MdPath);
            _settings.HtmlPath = EditorGUILayout.TextField(new GUIContent("HTML 경로", $"작업 기록 폴더 기준. 비우면 HTML/{folderName}_작업기록.html"), _settings.HtmlPath);
            _settings.Title = EditorGUILayout.TextField(new GUIContent("기록 제목", $"새 MD·HTML의 머리글. 비우면 {folderName}"), _settings.Title);
            _settings.DefaultLabels = EditorGUILayout.TextField(new GUIContent("기본 라벨", "기록 창에서 새 항목에 미리 채울 라벨 (쉼표로 구분)"), _settings.DefaultLabels);

            if (EditorGUI.EndChangeCheck())
                _isDirty = true;

            EditorGUILayout.HelpBox("MD·HTML이 없으면 처음 항목을 추가할 때 빈 틀로 새로 만듭니다. 이미 있는 파일에는 번호가 이어지도록 끝에 덧붙입니다.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!_isDirty))
                {
                    if (GUILayout.Button("되돌리기", GUILayout.Width(80f)))
                    {
                        _settings = WorkLogSettings.Load();
                        _isDirty = false;
                    }
                    if (GUILayout.Button("저장", GUILayout.Width(80f)))
                    {
                        _settings.Save();
                        _isDirty = false;
                    }
                }
            }
        }

        private static void DrawLocalSettings()
        {
            EditorGUILayout.LabelField("이 PC만의 설정 (UserSettings/WorkLogLocal.json · EditorPrefs, Git에 올라가지 않음)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                string root = EditorGUILayout.DelayedTextField(new GUIContent("작업 기록 폴더", "MD·HTML 폴더의 상위. 비우면 드라이브의 \"내 드라이브\\작업 기록\"을 자동으로 찾음"), WorkLogLocalSettings.Root);
                if (root != WorkLogLocalSettings.Root)
                    WorkLogLocalSettings.Root = root;

                if (GUILayout.Button("찾기", GUILayout.Width(50f)))
                {
                    string picked = EditorUtility.OpenFolderPanel("작업 기록 폴더", WorkLogLocalSettings.Root, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        WorkLogLocalSettings.Root = picked;
                        GUI.FocusControl(null);
                    }
                }
            }

            string python = EditorGUILayout.DelayedTextField(new GUIContent("Python 경로", "python 3.8 이상. 비우면 python → py → python3 순서로 자동 탐색"), WorkLogLocalSettings.PythonPathSetting);
            if (python != WorkLogLocalSettings.PythonPathSetting)
                WorkLogLocalSettings.PythonPathSetting = python;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("명령줄 도구 위치", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel("Library/WorkLog/Tools/worklog_add.py  (에디터를 켤 때마다 최신 도구로 복사됨)", EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("python 3.8 이상과 markdown 패키지(pip install markdown)가 필요합니다.", MessageType.None);
        }

        #endregion
    }
}
