# -*- coding: utf-8 -*-
"""
worklog_add.py 테스트. 실행: python "Tests~/test_worklog.py"

예전 엘라린 도구(add_worklog.py)와 결과 비교는 환경 변수가 있을 때만 합니다.
    WORKLOG_OLD_TOOL     예전 add_worklog.py 경로
    WORKLOG_SAMPLE_ROOT  비교에 쓸 작업 기록 폴더 (MD/WakeUp_GuildMaster_작업기록.md, HTML/작업_기록.html) — 복사본만 수정함
"""
import json, os, re, shutil, subprocess, sys, tempfile, unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.join(os.path.dirname(HERE), "Tools~", "worklog_add.py")
ENV = dict(os.environ, PYTHONIOENCODING="utf-8", PYTHONUTF8="1")
ENV.pop("WORKLOG_ROOT", None)

ENTRY_A = """### {n}. 테스트 항목 `Code` **굵게** (2026-11-12)
- **요청/증상:** 첫 줄 내용
- **바뀐 로직:**
  - 하위 1 `x.cs`
  - 하위 2
    - 더 깊은 하위
- **확인:** 끝
"""
ENTRY_B = """### {n}. 다른 날짜 항목 (2026-11-13)
- **요청:** 새 날짜 탭
- **백업:** `ClaudeCode/Backup/2026-11-13/x`
"""


def run(*args, cwd=None):
    p = subprocess.run([sys.executable, TOOL, *args], cwd=cwd, env=ENV, capture_output=True, text=True, encoding="utf-8")
    return p.returncode, p.stdout + p.stderr


def strip_time(s):
    return re.sub(r"마지막 갱신 \d{4}-\d{2}-\d{2} \d\d:\d\d", "마지막 갱신 X", s)


def read(p):
    with open(p, encoding="utf-8", newline="") as f:
        return f.read()


class Base(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="worklog_test_")
        self.project = os.path.join(self.tmp, "MyGame")
        self.root = os.path.join(self.tmp, "작업 기록")
        for d in ("Assets", "ProjectSettings", "UserSettings", "WorkLog/entries"):
            os.makedirs(os.path.join(self.project, d))
        os.makedirs(self.root)

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def entry(self, n, text):
        p = os.path.join(self.project, "WorkLog", "entries", f"entry{n}.md")
        with open(p, "w", encoding="utf-8") as f:
            f.write(text.format(n=n))
        return p

    def settings(self, **kw):
        with open(os.path.join(self.project, "ProjectSettings", "WorkLogSettings.json"), "w", encoding="utf-8") as f:
            json.dump(kw, f, ensure_ascii=False)


class EmptyLogTests(Base):
    def test_next_on_missing_files(self):
        code, out = run("--next", "--json", "--project", self.project, "--root", self.root)
        self.assertEqual(code, 0, out)
        info = json.loads(out)
        self.assertEqual(info["last"], 0)
        self.assertEqual(info["next"], 1)
        self.assertFalse(info["mdExists"])
        self.assertTrue(info["md"].endswith(os.path.join("MD", "MyGame_작업기록.md")))

    def test_dry_run_does_not_create_files(self):
        self.entry(1, ENTRY_A)
        code, out = run("1", "--dry-run", "--project", self.project, "--root", self.root)
        self.assertEqual(code, 0, out)
        self.assertIn("dry-run: 문제 없음", out)
        self.assertFalse(os.path.exists(os.path.join(self.root, "MD")))

    def test_first_entries_create_md_and_html(self):
        self.settings(title="내 게임")
        self.entry(1, ENTRY_A)
        self.entry(2, ENTRY_B)
        code, out = run("1", "--project", self.project, "--root", self.root)
        self.assertEqual(code, 0, out)
        code, out = run("WorkLog/entries/entry2.md", "--project", self.project, "--root", self.root)
        self.assertEqual(code, 0, out)
        self.assertIn("2번: MD 1개, HTML 1개", out)
        md = read(os.path.join(self.root, "MD", "MyGame_작업기록.md"))
        h = read(os.path.join(self.root, "HTML", "MyGame_작업기록.html"))
        self.assertTrue(md.startswith("# 내 게임 작업 기록"))
        self.assertRegex(md, r"## 목차\r?\n- 1\. 테스트 항목 .*\r?\n- 2\. 다른 날짜 항목 \(2026-11-13\)")
        self.assertLess(md.index("### 1."), md.index("### 2."))
        self.assertLess(md.index("### 2."), md.index("## 부록"))
        self.assertIn("> 최종 갱신: 2026-11-13", md)
        self.assertIn("<title>내 게임 작업 기록</title>", h)
        self.assertEqual(h.count('class="tab"'), 2)
        # 새 날짜 탭이 맨 앞
        self.assertLess(h.index('data-tab="log-2026-11-13"'), h.index('data-tab="log-2026-11-12"'))
        self.assertIn("1~1번 · 1개 항목", h)
        self.assertNotIn("__TITLE__", h)
        self.assertEqual(len(os.listdir(os.path.join(self.root, "_backup"))), 2)  # 두 번째 추가 때 MD·HTML 백업

    def test_wrong_number_stops(self):
        self.entry(5, ENTRY_A)
        code, out = run("5", "--project", self.project, "--root", self.root)
        self.assertNotEqual(code, 0)
        self.assertIn("번호가 이어지지 않습니다", out)

    def test_bad_line_stops(self):
        p = self.entry(1, ENTRY_A)
        with open(p, "a", encoding="utf-8") as f:
            f.write("형식 없는 줄\n")
        code, out = run("1", "--dry-run", "--project", self.project, "--root", self.root)
        self.assertNotEqual(code, 0)
        self.assertIn("형식이 맞지 않는 줄", out)

    def test_local_root_with_quotes_and_project_from_cwd(self):
        with open(os.path.join(self.project, "UserSettings", "WorkLogLocal.json"), "w", encoding="utf-8") as f:
            json.dump({"root": f'"{self.root}"'}, f, ensure_ascii=False)
        sub = os.path.join(self.project, "Assets")
        code, out = run("--next", "--json", cwd=sub)
        self.assertEqual(code, 0, out)
        self.assertEqual(os.path.normpath(json.loads(out)["root"]), os.path.normpath(self.root))

    def test_missing_root_folder_stops(self):
        code, out = run("--next", "--project", self.project, "--root", os.path.join(self.tmp, "없음"))
        self.assertNotEqual(code, 0)
        self.assertIn("작업 기록 폴더가 없습니다", out)

    def test_init(self):
        code, out = run("--init", "--project", self.project, "--root", self.root)
        self.assertEqual(code, 0, out)
        self.assertTrue(os.path.isfile(os.path.join(self.root, "HTML", "MyGame_작업기록.html")))
        code, out = run("--init", "--project", self.project, "--root", self.root)
        self.assertIn("이미 있습니다", out)


@unittest.skipUnless(os.environ.get("WORKLOG_OLD_TOOL") and os.environ.get("WORKLOG_SAMPLE_ROOT"), "비교용 환경 변수 없음")
class SameAsOldToolTests(Base):
    def test_same_output(self):
        sample = os.environ["WORKLOG_SAMPLE_ROOT"]
        old_root, new_root = os.path.join(self.tmp, "old"), os.path.join(self.tmp, "new")
        for r in (old_root, new_root):
            for rel in ("MD/WakeUp_GuildMaster_작업기록.md", "HTML/작업_기록.html"):
                os.makedirs(os.path.join(r, os.path.dirname(rel)), exist_ok=True)
                shutil.copy2(os.path.join(sample, rel), os.path.join(r, rel))
        self.settings(mdPath="MD/WakeUp_GuildMaster_작업기록.md", htmlPath="HTML/작업_기록.html")
        last = max(int(x) for x in re.findall(r"^### (\d+)\.", read(os.path.join(old_root, "MD/WakeUp_GuildMaster_작업기록.md")), flags=re.M))
        a, b = self.entry(last + 1, ENTRY_A), self.entry(last + 2, ENTRY_B)
        old = subprocess.run([sys.executable, os.environ["WORKLOG_OLD_TOOL"], a, b, "--root", old_root],
                             env=ENV, capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(old.returncode, 0, old.stdout + old.stderr)
        code, out = run(str(last + 1), str(last + 2), "--project", self.project, "--root", new_root)
        self.assertEqual(code, 0, out)
        self.assertEqual(old.stdout.strip(), out.strip())
        for rel in ("MD/WakeUp_GuildMaster_작업기록.md", "HTML/작업_기록.html"):
            self.assertEqual(strip_time(read(os.path.join(old_root, rel))), strip_time(read(os.path.join(new_root, rel))), rel)


if __name__ == "__main__":
    unittest.main(verbosity=2)
