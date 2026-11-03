#   SWeetJelly Save

Unity 6.3 LTS용 범용 key 저장 패키지다. 소비자가 소유하는 payload를 등록해 capture/restore하며, 저장 요청 직렬화·dirty 변경 보존·기본 원자적 파일 교체·Bootstrapper 수명 관리를 제공한다.

설치와 공개 API 사용법은 [README][readme], 1.x의 제거 API와 기존 파일·asset 이전은 [2.0 이전 안내][migration]를 따른다. 게임 level ID·플레이 시간·revision·timestamp는 소비 payload가 소유한다. 미등록 key는 보존되며 Unregister는 연결만 해제한다. key 영구 삭제는 등록 해제 후 DeleteKeyAsync, 전체 파일 삭제는 DeleteAsync를 기다리고 Result를 확인한다.

Editor Presets는 저장 bootstrap asset과 실행 중 driver를 명시적으로 선택한다. 적용과 적용 후 실행, legacy 이전, 현재 파일 삭제와 PlayerPrefs 전체 초기화는 별도 작업이다. Windows 정상 종료는 비동기 최종 저장 성공을 기다린다. Editor 중지·OS 강제 종료·모바일 중단에는 같은 보장이 없다.

[readme]: README.md
[migration]: Documentation~/migration-2.0.md
