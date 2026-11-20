using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 작업 기록 도구에 필요한 Python 3.8 이상과 markdown 패키지가 있는지 확인하고, 없으면 설치 안내 창을 띄웁니다.<br/>
    /// 에디터를 켤 때 한 번 확인합니다('다시 묻지 않기'를 고르면 자동 확인은 끄고, 메뉴 Tools → 작업 기록 → 환경 확인은 언제든 씀).
    /// </summary>
    /// <remarks>작성일: 2026-11-20</remarks>
    [InitializeOnLoad]
    public static class WorkLogEnvironmentCheck
    {
        #region [ 확인 결과 (Check Result) ]

        /// <summary>
        /// 환경 확인 결과.
        /// </summary>
        public enum Status
        {
            /// <summary>Python과 markdown이 모두 있음.</summary>
            Ok,

            /// <summary>Python 3.8 이상을 찾지 못함.</summary>
            PythonMissing,

            /// <summary>Python은 있지만 markdown 패키지가 없음.</summary>
            MarkdownMissing,
        }

        #endregion

        #region [ 상수 (Constants) ]

        private const string MENU_PATH = "Tools/작업 기록/환경 확인";
        private const string DIALOG_TITLE = "작업 기록 환경 확인";
        private const string DONT_ASK_KEY = "TeamElarin.WorkLog.Env.DontAsk";
        private const string SESSION_KEY = "TeamElarin.WorkLog.Env.CheckedThisSession";
        private const string WINGET_PYTHON_ID = "Python.Python.3.12";
        private const string PYTHON_DOWNLOAD_URL = "https://www.python.org/downloads/";
        private const int WINGET_PROBE_TIMEOUT_MS = 5000;
        private const int PIP_TIMEOUT_MS = 300000;

        #endregion

        #region [ 내부 상태 (Internal State) ]

        private static Task<string> _pipTask; // 결과: 성공이면 빈 문자열, 실패면 오류 내용

        #endregion

        #region [ 초기화 (Initialization) ]

        static WorkLogEnvironmentCheck()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += CheckOnStartup;
        }

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// 메뉴에서 지금 바로 확인합니다. 문제가 없어도 결과를 알려 줍니다.
        /// </summary>
        [MenuItem(MENU_PATH, false, 30)]
        public static void CheckNow()
        {
            string python;
            Status status = Check(true, out python);
            if (status == Status.Ok)
            {
                EditorUtility.DisplayDialog(DIALOG_TITLE, $"작업 기록 도구를 쓸 준비가 됐습니다.\n\nPython: {python}\nmarkdown 패키지: 설치됨", "확인");
                return;
            }
            ShowFixDialog(status, python, true);
        }

        /// <summary>
        /// Python과 markdown을 확인합니다. 설정에 Python 경로를 적었으면 그 경로를, 비워 두었으면 자동으로 찾은 Python을 봅니다.
        /// </summary>
        /// <param name="refresh">true면 자동 탐색 결과를 버리고 다시 찾음 (설치한 뒤 다시 확인할 때)</param>
        /// <param name="python">확인한 Python (못 찾았으면 마지막으로 시도한 이름)</param>
        public static Status Check(bool refresh, out string python)
        {
            string setting = WorkLogLocalSettings.PythonPathSetting;
            python = string.IsNullOrWhiteSpace(setting) ? WorkLogTools.FindPython(refresh) : setting;
            if (!WorkLogTools.IsUsablePython(python))
                return Status.PythonMissing;
            return WorkLogTools.HasMarkdown(python) ? Status.Ok : Status.MarkdownMissing;
        }

        #endregion

        #region [ 확인 흐름 (Check Flow) ]

        // 에디터를 켤 때 한 번 ('다시 묻지 않기'를 골랐으면 건너뜀)
        private static void CheckOnStartup()
        {
            if (SessionState.GetBool(SESSION_KEY, false))
                return;
            SessionState.SetBool(SESSION_KEY, true);
            if (EditorPrefs.GetBool(DONT_ASK_KEY, false))
                return;

            string python;
            Status status = Check(false, out python);
            if (status != Status.Ok)
                ShowFixDialog(status, python, false);
        }

        private static void ShowFixDialog(Status status, string python, bool isManual)
        {
            bool isPythonMissing = status == Status.PythonMissing;
            string message;
            string okLabel;
            if (isPythonMissing)
            {
                bool hasWinget = IsWingetAvailable();
                message = "작업 기록 도구를 쓰려면 Python 3.8 이상이 필요한데, 이 PC에서 찾지 못했습니다.\n\n"
                          + (hasWinget
                              ? "[Python 설치]를 누르면 새 창에서 winget으로 설치합니다. 창의 안내를 따라 주세요."
                              : "[Python 설치]를 누르면 다운로드 페이지를 엽니다. 설치할 때 'Add python.exe to PATH'를 켜 주세요.")
                          + "\n\n설치가 끝나면 Tools → 작업 기록 → 환경 확인을 눌러 markdown 패키지까지 확인하세요.";
                okLabel = "Python 설치";
            }
            else
            {
                message = $"Python은 찾았지만({python}) 작업 기록 도구에 필요한 markdown 패키지가 없습니다.\n\n"
                          + "[markdown 설치]를 누르면 'python -m pip install markdown'을 실행합니다. 끝나면 결과를 알려 드립니다.";
                okLabel = "markdown 설치";
            }

            // 0: 설치, 1: 나중에, 2: 다시 묻지 않기 (메뉴에서 연 경우는 '닫기'만)
            int choice = isManual
                ? (EditorUtility.DisplayDialog(DIALOG_TITLE, message, okLabel, "닫기") ? 0 : 1)
                : EditorUtility.DisplayDialogComplex(DIALOG_TITLE, message, okLabel, "나중에", "다시 묻지 않기");

            if (choice == 2)
            {
                EditorPrefs.SetBool(DONT_ASK_KEY, true);
                Debug.Log("[작업 기록] 에디터를 켤 때 환경 확인 안내를 띄우지 않습니다. 필요하면 Tools → 작업 기록 → 환경 확인을 누르세요.");
                return;
            }
            if (choice != 0)
                return;

            // 설치를 고르면 다음에 다시 안내하도록 '다시 묻지 않기'를 풂
            EditorPrefs.DeleteKey(DONT_ASK_KEY);
            if (isPythonMissing)
                InstallPython();
            else
                InstallMarkdown(python);
        }

        #endregion

        #region [ 설치 (Install) ]

        private static void InstallPython()
        {
            if (IsWingetAvailable())
            {
                try
                {
                    // 진행 상황과 동의 질문을 사용자가 직접 보도록 새 콘솔 창에서 실행
                    Process.Start(new ProcessStartInfo("winget", $"install --id {WINGET_PYTHON_ID} -e --source winget") { UseShellExecute = true });
                    Debug.Log("[작업 기록] Python 설치 창을 띄웠습니다. 설치가 끝나면 Tools → 작업 기록 → 환경 확인을 누르세요.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[작업 기록] winget 설치를 시작하지 못해 다운로드 페이지를 엽니다: " + e.Message);
                }
            }
            Application.OpenURL(PYTHON_DOWNLOAD_URL);
        }

        private static void InstallMarkdown(string python)
        {
            if (_pipTask != null)
                return;

            string workingDir = WorkLogSettings.ProjectRoot;
            _pipTask = Task.Run(() => RunPip(python, workingDir));
            EditorApplication.update += PollPip;
            Debug.Log("[작업 기록] markdown 패키지를 설치하는 중입니다…");
        }

        // 백그라운드 스레드에서 실행 (Unity API를 쓰지 않음). 권한 문제로 실패하면 --user로 한 번 더
        private static string RunPip(string python, string workingDir)
        {
            string output, error;
            int code = WorkLogTools.RunProcess(python, "-m pip install markdown", workingDir, out output, out error, PIP_TIMEOUT_MS);
            if (code == 0)
                return string.Empty;

            code = WorkLogTools.RunProcess(python, "-m pip install --user markdown", workingDir, out output, out error, PIP_TIMEOUT_MS);
            if (code == 0)
                return string.Empty;
            string detail = (error + "\n" + output).Trim();
            return detail.Length > 0 ? detail : "pip이 실패했습니다 (종료 코드 " + code + ").";
        }

        private static void PollPip()
        {
            if (_pipTask == null || !_pipTask.IsCompleted)
                return;

            EditorApplication.update -= PollPip;
            string failure = _pipTask.IsFaulted ? _pipTask.Exception.GetBaseException().Message : _pipTask.Result;
            _pipTask = null;

            if (failure.Length == 0)
            {
                Debug.Log("[작업 기록] markdown 패키지를 설치했습니다.");
                EditorUtility.DisplayDialog(DIALOG_TITLE, "markdown 패키지를 설치했습니다. 이제 작업 기록 도구를 쓸 수 있습니다.", "확인");
                return;
            }

            Debug.LogWarning("[작업 기록] markdown 패키지를 설치하지 못했습니다:\n" + failure);
            EditorUtility.DisplayDialog(DIALOG_TITLE, "markdown 패키지를 설치하지 못했습니다.\n명령 프롬프트에서 'python -m pip install markdown'을 직접 실행해 보세요.\n\n" + LastLines(failure, 6), "확인");
        }

        #endregion

        #region [ 내부 도우미 (Helpers) ]

        private static bool IsWingetAvailable()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return false;
            string output, error;
            return WorkLogTools.RunProcess("winget", "--version", WorkLogSettings.ProjectRoot, out output, out error, WINGET_PROBE_TIMEOUT_MS) == 0;
        }

        private static string LastLines(string text, int count)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            int start = Math.Max(0, lines.Length - count);
            return string.Join("\n", lines, start, lines.Length - start);
        }

        #endregion
    }
}
