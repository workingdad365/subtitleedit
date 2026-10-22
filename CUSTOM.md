# Subtitle Edit 커스텀 수정 기록

## 기준 버전

- Subtitle Edit `4.0.16`
- 기준 커밋: `f2e4b539ab0876985f8a8d6d1dc744e1cd712686`
- 대상: Windows / .NET Framework 4.8 / Release 빌드
- 커스텀 실행 파일 이름: `SubtitleEditCustom.exe`
- 설치 경로: `C:\SubtitleEdit`

## 1. WebVTT 하단 자막이 화면 중앙으로 이동하는 문제 수정

### 재현 조건

- 입력 파일: `D:\[Download]\추격자\추격자.vtt`
- 설정 파일: `C:\SubtitleEdit\Settings.xml`
- 일괄 변환 출력: SubRip (`.srt`)
- 활성화된 처리 옵션:
  - 청각 장애인용 텍스트 삭제
  - 일반적인 오류 수정하기

변환 시 다음 대사 앞에 화면 정중앙 정렬을 뜻하는 ASS 태그 `{\an5}`가 붙었다.

```text
{\an5}미진이 걔
오늘 감기 걸렸다고 쉬겠대요
```

해당 원본 큐의 시간 및 위치 설정:

```text
00:11:00.326 --> 00:11:02.578 position:50.00%,middle align:middle size:80.00% line:74.00%
```

### 원인

수정 대상은 `src/libse/SubtitleFormats/WebVTT.cs`의 `GetPositionInfo` 메서드이다.

직접적인 원인은 `middle` 문자열 해석이 아니라, 세로 위치인 `line:74.00%`를 중앙으로 분류하는 기존 임계값이었다. 기존 코드는 퍼센트 단위 세로 위치를 다음처럼 처리했다.

- 25% 미만: 상단
- 25% 이상 75% 미만: 중앙
- 75% 이상: 기본 하단

원본 파일의 하단 자막 위치는 1·2·3줄에 따라 84.67%, 79.33%, 74.00%로 배치되어 있었다. 따라서 3줄 자막의 시작 위치인 74%가 중앙으로 잘못 근사되어 `{\an5}`가 생성되었다. 원본에서 `line:74.00%`는 113개 큐에 사용되었다.

### 변경 내용

퍼센트 단위 세로 위치의 중앙 판정 상한을 다음과 같이 변경했다.

```diff
- else if (number < 75)
+ else if (number < 200.0 / 3.0)
```

- 화면 하단 1/3 영역(약 66.67% 이상)은 하단으로 처리한다.
- 가로 중앙의 하단 자막에는 별도 ASS 위치 태그를 생성하지 않는다.
- 실제 중앙 위치(`line:50%`)의 `{\an5}`는 유지한다.
- 기존 상단 판정(25% 미만)은 유지한다.
- 가로 위치 판정 및 퍼센트가 아닌 `line` 값 처리는 변경하지 않았다.
- 원본 위치 설정은 `Paragraph.Style`에 그대로 남긴다.

이 변경은 정밀한 WebVTT 좌표 렌더링 구현이 아니라, WebVTT 위치를 ASS의 제한된 정렬 위치로 변환할 때 사용하는 근사 기준의 수정이다. 결과물에서 모든 `{\an5}` 태그를 무조건 삭제하는 방식이 아니다. 공통 WebVTT 로더 수정이므로 일괄 변환 외의 WebVTT 읽기 경로에도 적용된다.

### 회귀 검증

추가 파일: `src/Tests/Core/WebVttPositionTest.cs`

검증 항목:

| 입력 위치 | 기대 결과 |
| --- | --- |
| `line:74.00%` | 기본 하단, 위치 태그 없음 |
| `line:79.33%` | 기본 하단, 위치 태그 없음 |
| `line:84.67%` | 기본 하단, 위치 태그 없음 |
| `line:66.67%` | 기본 하단, 위치 태그 없음 |
| `line:66.66%` | 중앙 태그 `{\an5}` 유지 |
| `line:50.00%` | 중앙 태그 `{\an5}` 유지 |
| `line:10.00%` | 상단 태그 `{\an8}` 유지 |
| 세로 위치 없이 `position:50.00%,middle align:middle`만 지정 | 기본 하단, 위치 태그 없음 |

테스트 프로젝트 컴파일은 성공했다. MSTest 실행기는 `System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1` 어셈블리 로딩 오류로 실행하지 못했다. 대신 빌드된 테스트 어셈블리를 PowerShell에서 로드하여 위 테스트 메서드를 직접 호출했으며, 8개 케이스의 assertion이 모두 통과했다. MSTest 실행기 자체가 정상 완료된 것은 아니다.

실제 파일 검증은 임시 폴더에 원본 VTT와 `Settings.xml` 사본, Release 빌드 결과물을 배치하고 다음 명령 형태로 수행했다.

```text
SubtitleEdit.exe /convert "<임시 폴더>\input.vtt" SubRip /RemoveTextForHI /FixCommonErrors /outputfolder:"<임시 출력 폴더>"
```

결과:

- 변환 프로세스 종료 코드: `0`
- 출력 SRT의 `{\an5}`: `0개`
- 출력 SRT의 `{\an8}`: `9개` — 상단 위치는 유지됨
- 문제 구간 출력:

```srt
106
00:11:00,326 --> 00:11:02,578
미진이 걔 오늘
감기 걸렸다고 쉬겠대요
```

원본 VTT와 원본 위치의 SRT는 검증 과정에서 덮어쓰지 않았다. GUI를 직접 조작한 검증은 아니며, 명령행 변환으로 확인했다.

## 빌드 방법

현재 PC에서 성공한 PowerShell 명령:

```powershell
cd G:\project\csharp\subtitleedit

$env:MSBuildSDKsPath = "C:\Program Files\dotnet\sdk\7.0.302\Sdks"

& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  SubtitleEdit.sln `
  -restore `
  -t:SubtitleEdit `
  -p:Configuration=Release `
  "-p:Platform=Any CPU" `
  -m
```

- `-t:SubtitleEdit`로 앱 및 필요한 의존 프로젝트만 빌드한다.
- 솔루션 전체 빌드는 위 도구 조합에서 테스트 프로젝트의 C# 12 요구사항 때문에 실패한다.
- 단순 `dotnet build`는 기존 WinForms 리소스 처리 오류로 실패했다.
- 루트의 `build.bat`는 `git pull` 및 배포 패키징 작업도 수행하므로 이번 작업에서는 사용하지 않았다.

주요 빌드 결과물:

```text
src\ui\bin\Release\net48\SubtitleEdit.exe
src\ui\bin\Release\net48\libse.dll
```

## 설치 폴더 적용 내역

Subtitle Edit 종료 후 기존 파일을 백업하고 다음과 같이 복사했다.

| 빌드 결과물 | 설치 대상 |
| --- | --- |
| `src\ui\bin\Release\net48\SubtitleEdit.exe` | `C:\SubtitleEdit\SubtitleEditCustom.exe` |
| `src\ui\bin\Release\net48\libse.dll` | `C:\SubtitleEdit\libse.dll` |

- 두 파일 모두 복사 후 SHA-256 해시가 빌드 결과물과 일치함을 확인했다.
- `C:\SubtitleEdit\Settings.xml`은 변경하지 않았다.
- 기존 `C:\SubtitleEdit\SubtitleEdit.exe` 파일은 변경하지 않았다.
- **핵심 수정은 `libse.dll`에 있으므로 EXE만 복사해서는 반영되지 않는다.**
- **`libse.dll`은 기존 앱과 커스텀 앱이 공유하므로, 기존 `SubtitleEdit.exe`에도 이번 동작 변경이 적용된다.**

교체 전 `SubtitleEditCustom.exe` 및 `libse.dll` 백업:

```text
C:\SubtitleEdit\backup-vtt-position-20261010-213000
```

되돌릴 때는 앱을 종료하고 위 백업 폴더의 두 파일을 `C:\SubtitleEdit`에 복원한다.
