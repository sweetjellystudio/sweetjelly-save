# SWeetJelly Save

`SWeetJelly Save`는 등록된 save object의 상태를 캡처하고, `SWeetJelly.Serialization`을 통해 직렬화한 뒤, 로드 시 다시 복원하는 Unity 저장 패키지입니다.

이 패키지는 저장 상태, 저장 흐름, Unity 생명주기 처리, Initialiser 연동을 분리합니다.

## 목적

프로젝트 시스템에서 다음과 같은 공통 저장 흐름이 필요할 때 사용합니다.

- 타입화된 save key
- 등록 기반 save object
- load/save/delete 생명주기
- dirty 상태 추적
- level id, game time, last exit time 같은 전역 저장 메타데이터
- `SaveBehaviour`를 통한 Unity 생명주기 자동 저장
- `SaveInitModule`을 통한 Initialiser 연동

이 패키지는 저수준 파일 직렬화를 직접 구현하지 않습니다. `ISerializationService`를 통해 `SWeetJelly.Serialization`에 의존합니다.

## 패키지 구조

```text
Runtime/
  Contract/
    ISaveService.cs
    ISaveObject.cs
    SaveKey.cs
    SaveErrors.cs

  Domain/
    GlobalSaveData.cs
    SavedDataContainer.cs
    SavedDataEntry.cs
    SaveObject.cs
    PrimitiveSaveObject.cs
    SimpleBoolSave.cs
    SimpleFloatSave.cs
    SimpleIntSave.cs
    SimpleStringSave.cs

  App/
    SaveService.cs

  Framework/
    SaveBehaviour.cs

  InitialiserIntegration/
    SaveInitModule.cs

Editor/
  SaveActionsMenu.cs
  SaveEditorUtility.cs
  SavePresetsWindow.cs
```

## 어셈블리

### `SWeetJelly.Save.Contract`

공개 저장 API를 정의합니다.

- `ISaveService`
- `ISaveObject`
- `ISaveObject<T>`
- `SaveKey`
- `SaveErrors`

게임플레이 코드는 보통 이 어셈블리에만 의존하면 됩니다.

### `SWeetJelly.Save.Domain`

직렬화 가능한 저장 데이터와 재사용 가능한 save object 구현을 포함합니다.

- `GlobalSaveData`
- `SavedDataContainer`
- `SavedDataEntry`
- `SaveObject<T>`
- `PrimitiveSaveObject<T>`
- `SimpleBoolSave`
- `SimpleFloatSave`
- `SimpleIntSave`
- `SimpleStringSave`

### `SWeetJelly.Save.App`

주 저장 흐름 구현을 포함합니다.

- `SaveService`

### `SWeetJelly.Save.Framework`

Unity 생명주기 연동을 포함합니다.

- `SaveBehaviour`

### `SWeetJelly.Save.InitialiserIntegration`

Save를 Initialiser 패키지에 연결합니다.

- `SaveInitModule`

참고: 현재 어셈블리 이름은 `SWeetJelly.Save.InitialiserIntegraion`입니다.

## 핵심 개념

### `SaveKey`

`SaveKey`는 save object를 식별합니다.

```csharp
SaveKey key = "player.coins";
```

키는 ordinal string equality로 비교됩니다. 비어 있거나 공백뿐인 키는 save object 구현에서 거부됩니다.

### `ISaveObject<T>`

Save object는 하나의 상태 값을 캡처하고 복원하는 방법을 노출합니다.

```csharp
public interface ISaveObject<T> : ISaveObject
{
    T Capture();
    void Restore(T value);
}
```

Save service는 값을 `SaveKey` 기준으로 저장합니다.

### Primitive Save Objects

이 패키지는 primitive 값용 단순 save object 구현을 제공합니다.

```csharp
var coins = new SimpleIntSave("player.coins", defaultValue: 0);

coins.Set(100);
int currentCoins = coins.Value;
```

사용 가능한 primitive object:

- `SimpleBoolSave`
- `SimpleFloatSave`
- `SimpleIntSave`
- `SimpleStringSave`

### `GlobalSaveData`

`GlobalSaveData`는 루트 저장 모델입니다.

저장하는 값:

- `BuildId`
- `LevelId`
- `SavedData`
- `SavedGameTime`
- `LastExitTime`

`SaveService`는 등록된 save object를 `SavedData`로 캡처합니다.

## 기본 사용법

대부분의 프로젝트는 Initialiser에서 `ISaveService`를 resolve해서 사용합니다.

```csharp
using SWeetJelly.Initialiser.Framework;
using SWeetJelly.Save.Contract;

var saveService = InitialiserBehaviour.Services.Resolve<ISaveService>();
```

로드 전이나 후에 save object를 등록합니다.

```csharp
var coins = new SimpleIntSave("player.coins", 0);

var registerResult = saveService.Register(coins);

if (registerResult.IsFailure)
    return;
```

저장 데이터를 로드합니다.

```csharp
var loadResult = await saveService.LoadAsync();

if (loadResult.IsFailure)
    return;
```

데이터를 변경하고 dirty 상태로 표시합니다.

```csharp
coins.Set(250);
saveService.MarkDirty();
```

저장합니다.

```csharp
var saveResult = await saveService.SaveAsync();

if (saveResult.IsFailure)
    return;
```

dirty 상태가 아니어도 강제로 저장합니다.

```csharp
var forceSaveResult = await saveService.SaveAsync(forceSave: true);

if (forceSaveResult.IsFailure)
    return;
```

저장 데이터를 삭제합니다.

```csharp
saveService.Delete();
```

## 저장 흐름

```text
Register save objects
  -> LoadAsync()
     -> persistent data path에 GlobalSaveData가 있으면 로드
     -> 저장 파일이 없으면 새 GlobalSaveData 생성
     -> saved entry 캐시
     -> 등록된 save object 복원
     -> Loaded 이벤트 발생

MarkDirty()
  -> SaveAsync()
     -> dirty가 아니고 강제 저장도 아니면 건너뜀
     -> 등록된 save object 캡처
     -> entry 직렬화
     -> game time과 exit time flush
     -> ISerializationService를 통해 GlobalSaveData 기록
     -> Saved 이벤트 발생
```

`LoadAsync()` 이후 save object를 등록하면, 일치하는 saved entry가 있을 때 `SaveService`가 즉시 복원합니다.

`Register`, `LoadAsync`, `SaveAsync`는 `Result` 값을 반환합니다. 구체적인 저장 실패는 `SaveErrors`로 표현됩니다.

## 이벤트

`ISaveService`는 생명주기 이벤트를 노출합니다.

```csharp
saveService.Loaded += OnLoaded;
saveService.Saved += OnSaved;
saveService.Deleted += OnDeleted;
```

## 게임 시간

`SaveService.UpdateTime(float time)`은 `GlobalSaveData`에 사용할 현재 런타임 시간을 갱신합니다.

`SaveBehaviour`는 load/save 작업 전에 `Time.time`을 전달합니다.

저장할 때 `GlobalSaveData.Flush(...)`는 누적 게임 시간과 `LastExitTime`을 저장합니다.

## Unity 생명주기 드라이버

`SaveBehaviour`는 저장 생명주기 동작을 처리하는 Unity `MonoBehaviour` 드라이버입니다.

지원 기능:

- `BeginLoad()`로 로드 시작
- 드라이버 코드에서 강제 저장
- 주기적 auto-save
- application pause 시 저장
- application quit 시 저장
- destroy 시 생명주기 작업 취소

`SaveBehaviour`는 `SaveService`로 construct 되어야 합니다.

```csharp
var behaviour = gameObject.AddComponent<SaveBehaviour>();
behaviour.Construct(saveService);
behaviour.Configure(
    useAutoSave: true,
    autoSaveInterval: 30f,
    saveOnApplicationPause: true,
    saveOnApplicationQuit: true);
behaviour.BeginLoad();
```

## Initialiser 연동

`SaveInitModule`은 Save를 Initialiser 시스템에 연결합니다.

설치 시 수행하는 작업:

1. `ISerializationService` resolve
2. `SaveService` 생성
3. holder object에서 `SaveBehaviour`를 찾거나 생성
4. behaviour 설정 및 construct
5. `ISaveService` 등록
6. 로드 시작

일반적인 모듈 순서:

```text
Serialization
Save
Project modules that register save objects
```

Save는 `ISerializationService`를 resolve하므로 Serialization이 Save보다 먼저 설치되어야 합니다.

## Custom Save Object

값의 capture/restore 로직이 필요할 때는 `SaveObject<T>`를 사용합니다.

```csharp
public sealed class PlayerPositionSave : SaveObject<Vector3>
{
    private readonly Transform target;

    public PlayerPositionSave(SaveKey key, Transform target)
        : base(key)
    {
        this.target = target;
    }

    public override Vector3 Capture()
    {
        return target.position;
    }

    public override void Restore(Vector3 value)
    {
        target.position = value;
    }
}
```

Save service에 등록합니다.

```csharp
var result = saveService.Register(new PlayerPositionSave("player.position", playerTransform));
```

## 설계 메모

- `SaveService`는 저장 orchestration, dirty 상태, 등록된 save object를 소유합니다.
- `ISerializationService`는 직렬화와 파일 persistence를 소유합니다.
- `SaveBehaviour`는 Unity 생명주기 trigger를 소유합니다.
- Save 작업은 `Result`를 반환하고, 패키지 소유 실패 설명에는 `SaveErrors`를 사용합니다.
- 게임플레이 코드는 concrete framework 타입이 아니라 `ISaveService`와 `ISaveObject<T>`에 의존하는 것이 좋습니다.
- `player.coins`, `settings.audio.volume`, `level.progress`처럼 안정적이고 namespaced 된 save key를 사용하세요.

## 현재 상태

현재 이 패키지는 다음을 제공합니다.

- save service contract
- save object contract
- primitive save object
- global save data model
- save/load/delete service flow
- Unity lifecycle save behaviour
- Initialiser integration module
