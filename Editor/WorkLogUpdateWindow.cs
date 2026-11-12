using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UpdateInfo = TeamElarin.WorkLog.Editor.WorkLogUpdateChecker.UpdateInfo;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 새 버전 알림 창. [업데이트]는 Package Manager로 새 태그를 설치하고, [이 버전 건너뛰기]는 그 버전을 다시 알리지 않습니다.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    public class WorkLogUpdateWindow : EditorWindow
    {
        #region [ 상수 (Constants) ]

        private static readonly Vector2 WINDOW_SIZE = new Vector2(440f, 160f);

        #endregion

        #region [ 내부 상태 (Internal State) ]

        private UpdateInfo _info;
        private string _changelogUrl = string.Empty;
        private AddRequest _request;
        private string _message = string.Empty;

        #endregion

        #region [ Unity 생명주기 (Unity Lifecycle) ]

        private void Update()
        {
            if (_request == null || !_request.IsCompleted)
                return;

            if (_request.Status == StatusCode.Success)
            {
                Debug.Log($"[작업 기록] {_info.LatestTag}(으)로 업데이트했습니다.");
                _request = null;
                Close();
                return;
            }

            _message = "업데이트하지 못했습니다: " + (_request.Error != null ? _request.Error.message : "알 수 없는 오류");
            _request = null;
            Repaint();
        }

        private void OnGUI()
        {
            if (_info == null)
            {
                // 도메인 리로드 뒤에는 확인 결과가 없으므로 닫음
                Close();
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"{_info.DisplayName} 새 버전 {_info.LatestTag}이 있습니다", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"지금 설치된 버전: v{_info.CurrentVersion}", EditorStyles.label);
            EditorGUILayout.LabelField(_info.InstallUrl, EditorStyles.miniLabel);

            if (_changelogUrl.Length > 0 && EditorGUILayout.LinkButton("변경 기록 보기"))
                Application.OpenURL(_changelogUrl);

            if (_message.Length > 0)
                EditorGUILayout.HelpBox(_message, MessageType.Warning);

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(_request != null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이 버전 건너뛰기", GUILayout.Height(24f)))
                {
                    WorkLogUpdateChecker.SkipVersion(_info.LatestTag);
                    Close();
                }
                if (GUILayout.Button("나중에", GUILayout.Height(24f)))
                    Close();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_request != null ? "업데이트 중…" : "업데이트", GUILayout.Width(110f), GUILayout.Height(24f)))
                {
                    _message = string.Empty;
                    _request = WorkLogUpdateChecker.Install(_info);
                }
            }
            EditorGUILayout.Space(8f);
        }

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// 알림 창을 엽니다.
        /// </summary>
        public static WorkLogUpdateWindow Open(UpdateInfo info)
        {
            var window = CreateInstance<WorkLogUpdateWindow>();
            window.titleContent = new GUIContent("작업 기록 업데이트");
            window._info = info;
            window._changelogUrl = WorkLogUpdateChecker.GetChangelogUrl(info);
            window.minSize = WINDOW_SIZE;
            window.maxSize = WINDOW_SIZE;
            window.ShowUtility();
            window.CenterOnMainWin();
            return window;
        }

        #endregion

        #region [ 내부 메서드 (Private Methods) ]

        private void CenterOnMainWin()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            position = new Rect(main.x + (main.width - WINDOW_SIZE.x) * 0.5f, main.y + (main.height - WINDOW_SIZE.y) * 0.5f, WINDOW_SIZE.x, WINDOW_SIZE.y);
        }

        #endregion
    }
}
