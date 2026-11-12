using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using ProcessResult = TeamElarin.WorkLog.Editor.WorkLogTools.ProcessResult;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 기록 창 (Tools → 작업 기록 → 기록 창).<br/>
    /// 다음 번호·오늘 날짜를 채운 항목을 쓰고, entryNNN.md로 저장한 뒤 검사(dry-run)·드라이브 MD·HTML에 추가합니다.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    public class WorkLogWindow : EditorWindow
    {
        #region [ 상수 (Constants) ]

        private const string MENU_PATH = "Tools/작업 기록/기록 창";
        private const float LABEL_WIDTH = 120f;

        #endregion

        #region [ 다음 번호 정보 (Next Info) ]

        // worklog_add.py --next --json 결과 (JsonUtility는 필드 이름을 그대로 키로 씀)
        [Serializable]
        private class NextInfo
        {
            public string root = string.Empty;
            public string md = string.Empty;
            public string html = string.Empty;
            public bool mdExists;
            public bool htmlExists;
            public string entriesDir = string.Empty;
            public string title = string.Empty;
            public int last;
            public int next;
        }

        private enum TaskKind
        {
            None,
            Next,
            DryRun,
            Add,
        }

        #endregion

        #region [ 내부 상태 (Internal State) ]

        [SerializeField] private WorkLogEntry _entry;
        [SerializeField] private string _log = string.Empty;

        private NextInfo _info;
        private Task<ProcessResult> _runningTask;
        private TaskKind _runningKind;
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private bool _hasLoadedFile;

        #endregion

        #region [ Unity 생명주기 (Unity Lifecycle) ]

        private void OnEnable()
        {
            titleContent = new GUIContent("작업 기록");
            minSize = new Vector2(520f, 480f);
            RefreshNext();
        }

        private void Update()
        {
            if (_runningTask == null || !_runningTask.IsCompleted)
                return;

            ProcessResult result = _runningTask.IsFaulted
                ? new ProcessResult { ExitCode = -1, Error = _runningTask.Exception.GetBaseException().Message }
                : _runningTask.Result;
            TaskKind kind = _runningKind;
            _runningTask = null;
            _runningKind = TaskKind.None;

            switch (kind)
            {
                case TaskKind.Next: OnNextFinished(result); break;
                case TaskKind.DryRun: OnDryRunFinished(result); break;
                case TaskKind.Add: OnAddFinished(result); break;
            }
            Repaint();
        }

        private void OnGUI()
        {
            bool isBusy = _runningTask != null;
            DrawLocation(isBusy);
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(isBusy || _entry == null))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                if (_entry != null)
                    DrawEntry();
                EditorGUILayout.EndScrollView();
                DrawButtons();
            }

            DrawLog(isBusy);
        }

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// 기록 창을 엽니다.
        /// </summary>
        [MenuItem(MENU_PATH, false, 10)]
        public static void Open()
        {
            GetWindow<WorkLogWindow>();
        }

        #endregion

        #region [ 화면 그리기 (Drawing) ]

        private void DrawLocation(bool isBusy)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string summary = _info == null
                    ? (isBusy ? "작업 기록 위치를 읽는 중…" : "작업 기록 위치를 읽지 못했습니다")
                    : $"마지막 {_info.last}번 · 다음 {_info.next}번";
                GUILayout.Label(summary, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(isBusy))
                {
                    if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton))
                        RefreshNext();
                }
                using (new EditorGUI.DisabledScope(_info == null || !_info.htmlExists))
                {
                    if (GUILayout.Button("HTML 열기", EditorStyles.toolbarButton))
                        Application.OpenURL(new Uri(_info.html).AbsoluteUri);
                }
                if (GUILayout.Button("설정", EditorStyles.toolbarButton))
                    WorkLogSettingsProvider.Open();
            }

            if (_info == null)
                return;

            EditorGUILayout.LabelField("MD", _info.md + (_info.mdExists ? string.Empty : "  (없음 · 처음 추가할 때 만듦)"), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("HTML", _info.html + (_info.htmlExists ? string.Empty : "  (없음 · 처음 추가할 때 만듦)"), EditorStyles.miniLabel);
        }

        private void DrawEntry()
        {
            EditorGUIUtility.labelWidth = LABEL_WIDTH;
            using (new EditorGUILayout.HorizontalScope())
            {
                _entry.Number = EditorGUILayout.IntField("번호", _entry.Number, GUILayout.Width(LABEL_WIDTH + 60f));
                GUILayout.Space(12f);
                EditorGUIUtility.labelWidth = 40f;
                _entry.Date = EditorGUILayout.TextField("날짜", _entry.Date, GUILayout.Width(180f));
                EditorGUIUtility.labelWidth = LABEL_WIDTH;
                GUILayout.FlexibleSpace();
                if (_hasLoadedFile)
                    GUILayout.Label("저장된 항목 파일을 불러옴", EditorStyles.miniLabel);
            }
            _entry.Title = EditorGUILayout.TextField("제목", _entry.Title);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("내용 (첫 줄은 라벨 옆, 나머지 줄은 하위 항목. 하위 항목은 \"- \"로 시작)", EditorStyles.miniLabel);

            int removeIndex = -1;
            for (int i = 0; i < _entry.Items.Count; i++)
            {
                WorkLogEntry.Item item = _entry.Items[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    item.Label = EditorGUILayout.TextField(item.Label, GUILayout.Width(LABEL_WIDTH - 4f));
                    item.Content = EditorGUILayout.TextArea(item.Content, GUILayout.MinHeight(38f));
                    if (GUILayout.Button("−", GUILayout.Width(22f)))
                        removeIndex = i;
                }
            }
            if (removeIndex >= 0)
                _entry.Items.RemoveAt(removeIndex);

            if (GUILayout.Button("+ 라벨 추가", GUILayout.Width(100f)))
                _entry.Items.Add(new WorkLogEntry.Item());
        }

        private void DrawButtons()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("새 항목으로 비우기", GUILayout.Height(26f)))
                    ResetEntry();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("저장", GUILayout.Width(80f), GUILayout.Height(26f)))
                    SaveEntry();
                if (GUILayout.Button("검사", GUILayout.Width(80f), GUILayout.Height(26f)))
                    RunWithEntry(TaskKind.DryRun, "--dry-run");
                if (GUILayout.Button("기록에 추가", GUILayout.Width(100f), GUILayout.Height(26f)))
                {
                    if (EditorUtility.DisplayDialog("작업 기록", $"{_entry.Number}번 항목을 작업 기록 MD·HTML에 추가할까요?\n(바꾸기 전 MD·HTML은 작업 기록 폴더의 _backup에 복사됩니다)", "추가", "취소"))
                        RunWithEntry(TaskKind.Add, string.Empty);
                }
            }
        }

        private void DrawLog(bool isBusy)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(isBusy ? "실행 중…" : "결과", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(90f));
            EditorGUILayout.SelectableLabel(_log, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region [ 실행 (Run Tasks) ]

        private void RefreshNext()
        {
            if (_runningTask != null)
                return;

            _runningKind = TaskKind.Next;
            _runningTask = WorkLogTools.RunAsync("--next --json");
        }

        private void RunWithEntry(TaskKind kind, string extraArguments)
        {
            string path = SaveEntry();
            if (string.IsNullOrEmpty(path))
                return;

            _runningKind = kind;
            _runningTask = WorkLogTools.RunAsync(WorkLogTools.Quote(path) + (string.IsNullOrEmpty(extraArguments) ? string.Empty : " " + extraArguments));
        }

        private void OnNextFinished(ProcessResult result)
        {
            if (result.ExitCode != 0)
            {
                _info = null;
                _log = "작업 기록 위치를 읽지 못했습니다.\n" + result.Error + result.Output;
                if (_entry == null)
                    _entry = WorkLogEntry.CreateNew(1, WorkLogSettings.Load().GetDefaultLabels());
                return;
            }

            try
            {
                _info = JsonUtility.FromJson<NextInfo>(result.Output);
            }
            catch (Exception e)
            {
                _info = null;
                _log = "작업 기록 위치를 읽지 못했습니다: " + e.Message + "\n" + result.Output;
                return;
            }

            // 입력 중인 내용이 없거나 이미 추가된 번호면 다음 번호로 새로 채움
            if (_entry == null || _entry.Number <= _info.last || IsBlank(_entry))
                ResetEntry();
        }

        private void OnDryRunFinished(ProcessResult result)
        {
            _log = (result.ExitCode == 0 ? "검사 통과\n" : "검사 실패\n") + Join(result);
        }

        private void OnAddFinished(ProcessResult result)
        {
            _log = (result.ExitCode == 0 ? "추가했습니다\n" : "추가하지 못했습니다\n") + Join(result);
            if (result.ExitCode != 0)
                return;

            _entry = null;
            RefreshNext();
        }

        #endregion

        #region [ 항목 파일 (Entry File) ]

        // 다음 번호의 항목 파일이 이미 있으면 불러오고, 없으면 기본 라벨로 새로 만듦
        private void ResetEntry()
        {
            WorkLogSettings settings = WorkLogSettings.Load();
            int number = _info != null ? _info.next : 1;
            _hasLoadedFile = false;

            string path = GetEntryPath(number);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                WorkLogEntry loaded = WorkLogEntry.Parse(File.ReadAllText(path, Encoding.UTF8));
                if (loaded != null && loaded.Number == number)
                {
                    _entry = loaded;
                    _hasLoadedFile = true;
                    return;
                }
            }
            _entry = WorkLogEntry.CreateNew(number, settings.GetDefaultLabels());
            GUI.FocusControl(null);
        }

        // 항목을 entryNNN.md로 저장하고 경로를 돌려줌. 형식 오류면 빈 문자열
        private string SaveEntry()
        {
            string problem = _entry.Validate();
            if (problem.Length > 0)
            {
                _log = "저장하지 않았습니다: " + problem;
                return string.Empty;
            }

            string path = GetEntryPath(_entry.Number);
            if (string.IsNullOrEmpty(path))
            {
                _log = "항목 폴더를 알 수 없습니다. 새로 고침을 누르거나 설정을 확인하세요.";
                return string.Empty;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, _entry.ToMarkdown(), new UTF8Encoding(false));
            _log = "저장했습니다: " + path;
            return path;
        }

        private string GetEntryPath(int number)
        {
            string dir = _info != null ? _info.entriesDir : string.Empty;
            if (string.IsNullOrEmpty(dir))
            {
                string setting = WorkLogSettings.Load().EntriesDir;
                dir = Path.Combine(WorkLogSettings.ProjectRoot, string.IsNullOrWhiteSpace(setting) ? WorkLogSettings.DEFAULT_ENTRIES_DIR : setting);
            }
            return Path.Combine(dir, $"entry{number}.md");
        }

        private static bool IsBlank(WorkLogEntry entry)
        {
            return entry.Title.Trim().Length == 0 && entry.Items.TrueForAll(i => i.Content.Trim().Length == 0);
        }

        private static string Join(ProcessResult result)
        {
            return (result.Output + result.Error).Trim();
        }

        #endregion
    }
}
