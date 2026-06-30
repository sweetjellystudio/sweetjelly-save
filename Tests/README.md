# Save Tests

이 폴더는 `com.sweetjelly.save` 패키지를 현재 Unity 테스트 프로젝트에서 검증하기 위한 테스트를 담는다.

## EditMode

`EditMode` 테스트는 Unity scene 실행 없이 save domain과 `SaveService`의 핵심 저장 흐름을 검증한다.

- `SaveDomainTest`
  - `SaveKey` equality와 string 변환을 검증한다.
  - primitive save object가 값을 capture/restore하는지 검증한다.
  - 잘못된 타입 restore가 `InvalidCastException`을 발생시키는지 검증한다.
  - `GlobalSaveData.Flush(...)`가 game time과 last exit time을 갱신하는지 검증한다.

- `SaveServiceTest`
  - 저장 파일이 없을 때 새 save data로 load되고 `Loaded` 이벤트가 발생하는지 검증한다.
  - load 이후 등록된 save object가 저장된 entry로 restore되는지 검증한다.
  - dirty 상태에서 save하면 등록된 save object가 capture되어 serializer에 전달되는지 검증한다.
  - dirty가 아니면 일반 save는 write하지 않고, force save는 write하는지 검증한다.
  - delete가 저장 데이터를 제거하고 상태와 이벤트를 갱신하는지 검증한다.
  - 중복 key 등록과 load 전 save 호출이 예외를 발생시키는지 검증한다.

- `SavePackageIntegrationTest`
  - `SavePackageBootstrapperSo`이 `ISerializationService`를 resolve해서 `SaveService`를 생성하는지 검증한다.
  - `SavePackageBootstrapperSo`이 holder object에 `SaveBehaviour`를 추가하고 construct하는지 검증한다.
  - `SavePackageBootstrapperSo`이 `ISaveService`를 `ServiceRegistry`에 등록하는지 검증한다.

## PlayMode

`PlayMode` 테스트는 Unity `MonoBehaviour` driver인 `SaveBehaviour`를 실제 GameObject에 붙여 검증한다.

- `SaveBehaviourFlowTest`
  - construct 및 load가 끝난 `SaveBehaviour`에서 `ForceSaveFromDriverAsync(...)`가 force save를 수행하는지 검증한다.

현재 테스트는 fake serialization service를 사용하므로 실제 파일 시스템에는 쓰지 않는다. 실제 persistent data path 저장은 serialization package의 file storage 테스트 또는 별도 platform integration 테스트에서 다룬다.
