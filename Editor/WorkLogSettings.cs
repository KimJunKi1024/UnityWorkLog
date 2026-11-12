using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 프로젝트 공용 설정 (<c>ProjectSettings/WorkLogSettings.json</c>, Git에 올려 팀이 같이 씀).<br/>
    /// 파이썬 도구(<c>Tools~/worklog_add.py</c>)도 같은 파일·같은 키를 읽습니다. 빈 값은 도구의 기본값을 씁니다.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    public class WorkLogSettings
    {
        #region [ 공개 프로퍼티 (Public Properties) ]

        /// <summary>항목(entryNNN.md) 폴더. 프로젝트 폴더 기준. 비우면 <c>WorkLog/entries</c>.</summary>
        public string EntriesDir { get; set; } = string.Empty;

        /// <summary>작업 기록 폴더 기준 MD 경로. 비우면 <c>MD/&lt;프로젝트 폴더 이름&gt;_작업기록.md</c>.</summary>
        public string MdPath { get; set; } = string.Empty;

        /// <summary>작업 기록 폴더 기준 HTML 경로. 비우면 <c>HTML/&lt;프로젝트 폴더 이름&gt;_작업기록.html</c>.</summary>
        public string HtmlPath { get; set; } = string.Empty;

        /// <summary>기록 제목 (새 MD·HTML 머리글). 비우면 프로젝트 폴더 이름.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>기록 창에서 새 항목을 만들 때 미리 채울 라벨 (쉼표로 구분).</summary>
        public string DefaultLabels { get; set; } = DEFAULT_LABELS;

        /// <summary>설정 파일 전체 경로.</summary>
        public static string FilePath => Path.Combine(ProjectRoot, "ProjectSettings", FILE_NAME);

        /// <summary>Unity 프로젝트 폴더 (Assets의 부모).</summary>
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>프로젝트 폴더 이름 (기본값 안내에 씀).</summary>
        public static string ProjectFolderName => Path.GetFileName(ProjectRoot);

        #endregion

        #region [ 상수 (Constants) ]

        /// <summary>기본 라벨 목록.</summary>
        public const string DEFAULT_LABELS = "요청/증상, 기존 로직, 바뀐 로직, 이유, 확인, 백업";

        /// <summary>항목 폴더 기본값 (파이썬 도구와 같음).</summary>
        public const string DEFAULT_ENTRIES_DIR = "WorkLog/entries";

        private const string FILE_NAME = "WorkLogSettings.json";

        #endregion

        #region [ JSON 형식 (JSON Shape) ]

        // 파이썬 도구와 키 이름을 맞추기 위한 저장 형식 (JsonUtility는 필드 이름을 그대로 키로 씀)
        [Serializable]
        private class FileData
        {
            public string entriesDir = string.Empty;
            public string mdPath = string.Empty;
            public string htmlPath = string.Empty;
            public string title = string.Empty;
            public string defaultLabels = DEFAULT_LABELS;
        }

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// 설정 파일을 읽습니다. 없거나 읽지 못하면 기본값을 돌려줍니다.
        /// </summary>
        public static WorkLogSettings Load()
        {
            var settings = new WorkLogSettings();
            if (!File.Exists(FilePath))
                return settings;

            try
            {
                FileData data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (data == null)
                    return settings;

                settings.EntriesDir = data.entriesDir ?? string.Empty;
                settings.MdPath = data.mdPath ?? string.Empty;
                settings.HtmlPath = data.htmlPath ?? string.Empty;
                settings.Title = data.title ?? string.Empty;
                settings.DefaultLabels = string.IsNullOrWhiteSpace(data.defaultLabels) ? DEFAULT_LABELS : data.defaultLabels;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[작업 기록] 설정 파일을 읽지 못해 기본값을 씁니다: " + e.Message);
            }
            return settings;
        }

        /// <summary>
        /// 설정 파일에 저장합니다 (UTF-8, BOM 없음).
        /// </summary>
        public void Save()
        {
            var data = new FileData
            {
                entriesDir = Clean(EntriesDir),
                mdPath = Clean(MdPath),
                htmlPath = Clean(HtmlPath),
                title = Clean(Title),
                defaultLabels = string.IsNullOrWhiteSpace(DefaultLabels) ? DEFAULT_LABELS : DefaultLabels.Trim(),
            };
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true) + "\n", new UTF8Encoding(false));
        }

        /// <summary>
        /// 기본 라벨을 목록으로 나눕니다.
        /// </summary>
        public string[] GetDefaultLabels()
        {
            string[] parts = (string.IsNullOrWhiteSpace(DefaultLabels) ? DEFAULT_LABELS : DefaultLabels).Split(',');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return Array.FindAll(parts, p => p.Length > 0);
        }

        /// <summary>
        /// 경로 값에서 앞뒤 공백과 따옴표를 뺍니다.
        /// </summary>
        public static string Clean(string value)
        {
            return (value ?? string.Empty).Trim().Trim('"', '\'').Trim();
        }

        #endregion
    }
}
