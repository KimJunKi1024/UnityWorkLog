using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TeamElarin.WorkLog.Editor
{
    /// <summary>
    /// 작업 기록 항목 하나 (entryNNN.md).<br/>
    /// 형식: 첫 줄 <c>### 번호. 제목 (날짜)</c>, 그 아래 <c>- **라벨:** 내용</c>과 공백 2칸으로 들여쓴 하위 줄.
    /// </summary>
    /// <remarks>작성일: 2026-11-12</remarks>
    [Serializable]
    public class WorkLogEntry
    {
        #region [ 항목 내용 (Entry Item) ]

        /// <summary>
        /// 라벨 하나와 그 내용. 내용의 첫 줄은 라벨 옆에, 나머지 줄은 하위 줄(공백 2칸 들여쓰기)로 저장합니다.
        /// </summary>
        [Serializable]
        public class Item
        {
            /// <summary>라벨 (예: 요청/증상).</summary>
            public string Label = string.Empty;

            /// <summary>내용 (여러 줄 가능).</summary>
            public string Content = string.Empty;
        }

        #endregion

        #region [ 공개 필드 (Public Fields) ]

        // EditorWindow가 도메인 리로드 뒤에도 입력 중인 내용을 지키도록 직렬화 가능한 필드로 둠

        /// <summary>항목 번호.</summary>
        public int Number;

        /// <summary>제목.</summary>
        public string Title = string.Empty;

        /// <summary>날짜 (예: 2026-11-12 또는 2026-11-12~13).</summary>
        public string Date = string.Empty;

        /// <summary>라벨 목록.</summary>
        public List<Item> Items = new List<Item>();

        #endregion

        #region [ 상수 (Constants) ]

        private static readonly Regex HEAD_REGEX = new Regex(@"^### (\d+)\. (.+) \((\d{4}-\d{2}-[0-9~]+)\)\s*$");
        private static readonly Regex ITEM_REGEX = new Regex(@"^- \*\*(.+?):\*\*\s?(.*)$");
        private static readonly Regex DATE_REGEX = new Regex(@"^\d{4}-\d{2}-[0-9~]+$");
        private const string SUB_INDENT = "  ";

        #endregion

        #region [ 공개 메서드 (Public Methods) ]

        /// <summary>
        /// 기본 라벨로 채운 새 항목을 만듭니다.
        /// </summary>
        public static WorkLogEntry CreateNew(int number, string[] labels)
        {
            var entry = new WorkLogEntry { Number = number, Date = DateTime.Now.ToString("yyyy-MM-dd") };
            foreach (string label in labels)
                entry.Items.Add(new Item { Label = label });
            return entry;
        }

        /// <summary>
        /// 항목 파일 내용을 읽습니다. 첫 줄 형식이 맞지 않으면 null.
        /// </summary>
        public static WorkLogEntry Parse(string text)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Trim('\n', '﻿').Split('\n');
            Match head = HEAD_REGEX.Match(lines[0]);
            if (!head.Success)
                return null;

            var entry = new WorkLogEntry
            {
                Number = int.Parse(head.Groups[1].Value),
                Title = head.Groups[2].Value,
                Date = head.Groups[3].Value,
            };

            Item current = null;
            var content = new StringBuilder();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                Match item = ITEM_REGEX.Match(line);
                if (item.Success)
                {
                    Flush(entry, current, content);
                    current = new Item { Label = item.Groups[1].Value };
                    content.Clear().Append(item.Groups[2].Value);
                }
                else if (current != null)
                {
                    content.Append('\n').Append(line.StartsWith(SUB_INDENT, StringComparison.Ordinal) ? line.Substring(SUB_INDENT.Length) : line);
                }
                else if (line.Trim().Length > 0)
                {
                    // 라벨 앞의 형식 없는 줄은 '메모' 라벨로 보존
                    current = new Item { Label = "메모" };
                    content.Clear().Append(line);
                }
            }
            Flush(entry, current, content);
            foreach (Item it in entry.Items)
                it.Content = it.Content.TrimEnd();
            return entry;
        }

        /// <summary>
        /// 항목 파일 내용으로 만듭니다 (줄바꿈 LF, 끝에 줄바꿈 하나).
        /// </summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.Append("### ").Append(Number).Append(". ").Append(Title.Trim()).Append(" (").Append(Date.Trim()).Append(")\n");
            foreach (Item item in Items)
            {
                // 라벨이나 내용이 빈 줄은 기록에 넣지 않음
                string label = item.Label.Trim();
                if (label.Length == 0 || (item.Content ?? string.Empty).Trim().Length == 0)
                    continue;

                string[] lines = (item.Content ?? string.Empty).Replace("\r\n", "\n").TrimEnd().Split('\n');
                bool startsWithSub = lines[0].TrimStart().StartsWith("- ", StringComparison.Ordinal);
                sb.Append("- **").Append(label).Append(":**");
                int first = 0;
                if (!startsWithSub)
                {
                    if (lines[0].Trim().Length > 0)
                        sb.Append(' ').Append(lines[0].Trim());
                    first = 1;
                }
                sb.Append('\n');
                for (int i = first; i < lines.Length; i++)
                {
                    if (lines[i].Trim().Length == 0)
                        continue;
                    sb.Append(SUB_INDENT).Append(lines[i].TrimEnd()).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 저장 전에 고쳐야 할 점을 돌려줍니다. 문제가 없으면 빈 문자열.
        /// </summary>
        public string Validate()
        {
            if (Number <= 0)
                return "번호는 1 이상이어야 합니다.";
            if (Title.Trim().Length == 0)
                return "제목을 입력하세요.";
            if (Title.Contains("\n"))
                return "제목은 한 줄이어야 합니다.";
            if (!DATE_REGEX.IsMatch(Date.Trim()))
                return "날짜는 2026-11-12 또는 2026-11-12~13 형식이어야 합니다.";
            if (Items.TrueForAll(i => i.Label.Trim().Length == 0 || i.Content.Trim().Length == 0))
                return "내용이 있는 라벨이 하나 이상 있어야 합니다.";
            foreach (Item item in Items)
            {
                if (item.Label.Contains(":") || item.Label.Contains("*") || item.Label.Contains("\n"))
                    return $"라벨 '{item.Label}'에는 콜론(:)·별표(*)·줄바꿈을 쓸 수 없습니다.";
            }
            return string.Empty;
        }

        #endregion

        #region [ 내부 메서드 (Private Methods) ]

        private static void Flush(WorkLogEntry entry, Item current, StringBuilder content)
        {
            if (current == null)
                return;
            current.Content = content.ToString();
            entry.Items.Add(current);
        }

        #endregion
    }
}
