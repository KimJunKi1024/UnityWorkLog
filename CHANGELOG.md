# 변경 기록

## [1.0.0] - 2026-11-12
- 첫 공개: 기록 창(Tools → 작업 기록 → 기록 창), 설정 화면(Tools → 작업 기록 → 설정), 파이썬 도구 `worklog_add.py`
- 엘라린 프로젝트의 `add_worklog.py`를 일반화
  - MD·HTML 경로, 항목 폴더, 기록 제목을 설정으로 (`ProjectSettings/WorkLogSettings.json`)
  - 작업 기록 폴더는 PC별 설정 (`UserSettings/WorkLogLocal.json`) 또는 드라이브 자동 탐색
  - MD·HTML이 없으면 빈 틀로 새로 만듦, `--next`(다음 번호), `--init`(빈 파일만 만들기), 번호만으로 항목 지정
  - 이미 있는 기록에 추가한 결과는 예전 도구와 같음 (CRLF MD의 목차 줄바꿈만 바르게 고침)
