# Work Log (Unity 작업 기록 도구)

작업 하나가 끝날 때마다 쓰는 **작업 기록 항목**(`entryNNN.md`)을 구글 드라이브 같은 공용 폴더의
**작업 기록 MD와 HTML에 한 번에 추가**하는 도구입니다.

- 번호는 MD의 마지막 번호 + 1이어야 하며, 아니면 중단합니다(중복·빠짐 방지).
- HTML은 날짜별 탭으로 나뉘고, 탭이 없으면 새로 만들어 맨 앞에 둡니다.
- MD·HTML이 없으면 처음 추가할 때 빈 틀로 새로 만듭니다.
- 바꾸기 전 MD·HTML은 작업 기록 폴더의 `_backup/`에 시간 이름으로 복사합니다.
- 커밋 도구 [Git Sync](https://github.com/KimJunKi1024/UnityGitSync)와 함께 쓰면, Git Sync 설정의 **작업 기록 폴더**를 이 도구의 **항목 폴더**와 같게 맞추면 새 항목 제목이 커밋 설명에 들어갑니다.

## 필요한 것
- Unity 2021.3 이상
- Python 3.8 이상과 `markdown` 패키지

```
pip install markdown
```

둘 중 하나가 없으면 에디터를 켤 때 안내 창이 뜹니다.
- Python이 없으면 **[Python 설치]**: Windows에서 winget이 있으면 새 창에서 설치하고, 없으면 다운로드 페이지를 엽니다. 표준 위치에 설치하면 Unity를 다시 켜지 않아도 찾습니다.
- markdown이 없으면 **[markdown 설치]**: `python -m pip install markdown`을 실행하고 결과를 알려 줍니다(권한 문제면 `--user`로 다시 시도).
- **다시 묻지 않기**를 고르면 자동 안내는 끕니다. 언제든 **Tools → 작업 기록 → 환경 확인**으로 직접 확인할 수 있습니다.

## 설치
Unity에서 **Window → Package Manager → 왼쪽 위 + → Add package from git URL…** 에 다음을 넣습니다.

```
https://github.com/KimJunKi1024/UnityWorkLog.git
```

특정 버전으로 고정하려면 끝에 `#v1.1.0`처럼 태그를 붙입니다.
또는 `Packages/manifest.json`의 `dependencies`에 직접 넣어도 됩니다.

```json
"com.teamelarin.worklog": "https://github.com/KimJunKi1024/UnityWorkLog.git#v1.1.0"
```

## 설정
**Tools → 작업 기록 → 설정** (또는 Edit → Project Settings → Work Log)

| 항목 | 저장 위치 | 기본값 (비웠을 때) | 설명 |
|---|---|---|---|
| 항목 폴더 | `ProjectSettings/WorkLogSettings.json` | `WorkLog/entries` | `entryNNN.md`를 저장하는 폴더 (프로젝트 폴더 기준) |
| MD 경로 | 〃 | `MD/<프로젝트 폴더 이름>_작업기록.md` | 작업 기록 폴더 기준 |
| HTML 경로 | 〃 | `HTML/<프로젝트 폴더 이름>_작업기록.html` | 작업 기록 폴더 기준 |
| 기록 제목 | 〃 | 프로젝트 폴더 이름 | 새로 만드는 MD·HTML의 머리글 |
| 기본 라벨 | 〃 | `요청/증상, 기존 로직, 바뀐 로직, 이유, 확인, 백업` | 기록 창의 새 항목에 미리 채울 라벨 |
| 작업 기록 폴더 | `UserSettings/WorkLogLocal.json` (PC별) | 자동 탐색 | MD·HTML의 상위 폴더. 비우면 `D:`~`Z:`의 `내 드라이브\작업 기록`(또는 `My Drive\작업 기록`)을 찾음 |
| Python 경로 | EditorPrefs (PC별) | 자동 탐색 | python → py → python3 |

`ProjectSettings/WorkLogSettings.json`은 **Git에 올려 팀이 같이 쓰고**, `UserSettings/`는 PC마다 따로 둡니다(보통 Git 제외).

작업 기록 폴더를 찾는 순서: `--root` → 환경 변수 `WORKLOG_ROOT` → PC별 설정 → 자동 탐색.

## 기록 창
**Tools → 작업 기록 → 기록 창**

- 위쪽에 MD·HTML 위치와 마지막·다음 번호가 나옵니다. 다음 번호의 항목 파일이 이미 있으면 불러옵니다.
- 번호·날짜·제목과 라벨별 내용을 씁니다. 내용의 첫 줄은 라벨 옆에, 나머지 줄은 하위 항목이 됩니다(하위 항목은 `- `로 시작).
- **저장**: `entryNNN.md`로 저장 / **검사**: 저장 + 형식·번호 검사(`--dry-run`) / **기록에 추가**: 저장 + MD·HTML에 추가.

## 새 버전 알림
Git 주소로 설치했다면 에디터를 켤 때 **하루 한 번** 설치한 저장소의 `vX.Y.Z` 태그를 확인합니다(`git ls-remote`, 20초 제한).
지금 버전보다 높은 태그가 있으면 콘솔에 한 줄을 남기고 알림 창을 띄웁니다.

- **업데이트**: Package Manager로 `주소#새 태그`를 설치합니다 (`Packages/manifest.json`이 함께 바뀜).
- **이 버전 건너뛰기**: 그 버전은 다시 알리지 않습니다 (더 새 버전이 나오면 다시 알림).
- **나중에**: 다음 확인 때 다시 알립니다.

언제든 **Tools → 작업 기록 → 업데이트 확인**으로 직접 확인할 수 있습니다. 로컬 폴더(`file:`)로 설치한 경우는 확인하지 않습니다.
1.1.0보다 이전 버전에는 이 기능이 없으므로 한 번은 직접 태그를 바꿔 올려야 합니다.

## 명령줄 (Claude Code 등)
에디터를 켜면 도구가 `Library/WorkLog/Tools/`에 복사됩니다. 프로젝트 폴더에서 실행합니다.

```
python Library/WorkLog/Tools/worklog_add.py --next                       # 위치와 다음 번호
python Library/WorkLog/Tools/worklog_add.py 158 --dry-run                # 항목 폴더의 entry158.md 검사만
python Library/WorkLog/Tools/worklog_add.py 158                          # 실제 추가
python Library/WorkLog/Tools/worklog_add.py path/to/entry158.md          # 파일 경로로
python Library/WorkLog/Tools/worklog_add.py 158 159                      # 여러 개
python Library/WorkLog/Tools/worklog_add.py --init                       # 빈 MD·HTML만 만들기
```

## 항목 파일 형식 (한 파일에 항목 하나)
```
### 158. 제목 (2026-10-27)
- **요청/증상:** 무엇을 요청했는지
- **바뀐 로직:**
  - 하위 항목은 공백 2칸 + "- "
  - `코드`, **굵게** 사용 가능
- **확인:** 실제로 확인한 것만
- **백업:** `Backup/2026-10-27/작업명`
```
- 제목 줄 괄호 안에는 **날짜만** (`2026-10-27`, `2026-10-24~25`).
- 글머리는 반드시 `- **라벨:** 내용` (콜론이 굵게 **안쪽**).
- 번호는 MD의 마지막 번호 + 1이어야 합니다.

## 테스트
```
python "Tests~/test_worklog.py"
```
