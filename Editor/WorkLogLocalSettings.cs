using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 이 PC만의 설정 (Git에 올라가지 않음).<br/>
    /// 작업 기록 폴더는 파이썬 도구도 읽도록 <c>UserSettings/WorkLogLocal.json</c>에, Python 경로는 EditorPrefs에 둡니다.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    public static class WorkLogLocalSettings
    {
        #region [ 공개 프로퍼티 (Public Properties) ]

        /// <summary>작업 기록 폴더 (MD·HTML의 상위). 비우면 드라이브의 "내 드라이브\작업 기록"을 자동으로 찾습니다.</summary>
        public static string Root
        {
            get => ReadRoot();
            set => WriteRoot(WorkLogSettings.Clean(value));
        }

        /// <summary>직접 적은 Python 경로. 비우면 자동 탐색.</summary>
        public static string PythonPathSetting
        {
            get => EditorPrefs.GetString(PYTHON_KEY, string.Empty);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    EditorPrefs.DeleteKey(PYTHON_KEY);
                else
                    EditorPrefs.SetString(PYTHON_KEY, WorkLogSettings.Clean(value));
            }
        }

        /// <summary>실제로 실행할 Python. 직접 적은 경로가 있으면 그것, 없으면 자동으로 찾은 것.</summary>
        public static string PythonPath => string.IsNullOrWhiteSpace(PythonPathSetting) ? WorkLogTools.FindPython() : PythonPathSetting;

        /// <summary>설정 파일 전체 경로.</summary>
        public static string FilePath => Path.Combine(WorkLogSettings.ProjectRoot, "UserSettings", FILE_NAME);

        #endregion

        #region [ 상수 (Constants) ]

        private const string FILE_NAME = "WorkLogLocal.json";
        private const string PYTHON_KEY = "TeamElarin.WorkLog.PythonPath";

        #endregion

        #region [ JSON 형식 (JSON Shape) ]

        [Serializable]
        private class FileData
        {
            public string root = string.Empty;
        }

        #endregion

        #region [ 내부 메서드 (Private Methods) ]

        private static string ReadRoot()
        {
            if (!File.Exists(FilePath))
                return string.Empty;

            try
            {
                FileData data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath, Encoding.UTF8));
                return data == null ? string.Empty : data.root ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[작업 기록] PC별 설정을 읽지 못했습니다: " + e.Message);
                return string.Empty;
            }
        }

        private static void WriteRoot(string root)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var data = new FileData { root = root };
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true) + "\n", new UTF8Encoding(false));
        }

        #endregion
    }
}
