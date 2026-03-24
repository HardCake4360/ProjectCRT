$root = 'C:\Users\user\Documents\Unreal Projects\ProjectCRT_Unreal'

$subsystemH = @'
#pragma once

#include "CoreMinimal.h"
#include "Dialogue/ProjectCRTDialogueTypes.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ProjectCRTDialogueSubsystem.generated.h"

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FProjectCRTDialogueStartedSignature, UProjectCRTDialogueDataAsset*, DialogueAsset);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FProjectCRTDialogueLineChangedSignature, FProjectCRTDialogueLine, DialogueLine);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FProjectCRTDialogueEndedSignature);

UCLASS(BlueprintType)
class PROJECTCRT_UNREAL_API UProjectCRTDialogueSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	bool StartDialogue(UProjectCRTDialogueDataAsset* InDialogueAsset, int32 StartLineIndex = 0);

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	bool AdvanceDialogue();

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	bool SelectChoice(int32 ChoiceIndex);

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	void EndDialogue();

	UFUNCTION(BlueprintPure, Category = "Dialogue")
	bool HasActiveDialogue() const;

	UFUNCTION(BlueprintPure, Category = "Dialogue")
	bool IsAwaitingChoice() const;

	UFUNCTION(BlueprintPure, Category = "Dialogue")
	int32 GetCurrentLineIndex() const;

	UFUNCTION(BlueprintPure, Category = "Dialogue")
	UProjectCRTDialogueDataAsset* GetCurrentDialogueAsset() const;

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	bool GetCurrentLineData(FProjectCRTDialogueLine& OutDialogueLine) const;

	UPROPERTY(BlueprintAssignable, Category = "Dialogue")
	FProjectCRTDialogueStartedSignature OnDialogueStarted;

	UPROPERTY(BlueprintAssignable, Category = "Dialogue")
	FProjectCRTDialogueLineChangedSignature OnDialogueLineChanged;

	UPROPERTY(BlueprintAssignable, Category = "Dialogue")
	FProjectCRTDialogueEndedSignature OnDialogueEnded;

private:
	bool MoveToLine(int32 NewLineIndex);
	void BroadcastCurrentLine();
	const FProjectCRTDialogueLine* GetCurrentLine() const;

	UPROPERTY()
	TObjectPtr<UProjectCRTDialogueDataAsset> CurrentDialogueAsset = nullptr;

	int32 CurrentLineIndex = INDEX_NONE;
	bool bHasActiveDialogue = false;
	bool bAwaitingChoice = false;
};
'@

$subsystemCpp = @'
#include "Core/ProjectCRTDialogueSubsystem.h"

void UProjectCRTDialogueSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	UE_LOG(LogTemp, Log, TEXT("ProjectCRTDialogueSubsystem initialized."));
}

void UProjectCRTDialogueSubsystem::Deinitialize()
{
	EndDialogue();

	UE_LOG(LogTemp, Log, TEXT("ProjectCRTDialogueSubsystem deinitialized."));

	Super::Deinitialize();
}

bool UProjectCRTDialogueSubsystem::StartDialogue(UProjectCRTDialogueDataAsset* InDialogueAsset, int32 StartLineIndex)
{
	if (InDialogueAsset == nullptr || !InDialogueAsset->IsValidLineIndex(StartLineIndex))
	{
		return false;
	}

	CurrentDialogueAsset = InDialogueAsset;
	CurrentLineIndex = StartLineIndex;
	bHasActiveDialogue = true;
	bAwaitingChoice = false;

	OnDialogueStarted.Broadcast(CurrentDialogueAsset);
	BroadcastCurrentLine();
	return true;
}

bool UProjectCRTDialogueSubsystem::AdvanceDialogue()
{
	const FProjectCRTDialogueLine* CurrentLine = GetCurrentLine();
	if (!bHasActiveDialogue || CurrentLine == nullptr)
	{
		return false;
	}

	if (CurrentLine->Choices.Num() > 0)
	{
		bAwaitingChoice = true;
		return false;
	}

	if (CurrentLine->NextLineIndex == INDEX_NONE)
	{
		EndDialogue();
		return true;
	}

	return MoveToLine(CurrentLine->NextLineIndex);
}

bool UProjectCRTDialogueSubsystem::SelectChoice(int32 ChoiceIndex)
{
	const FProjectCRTDialogueLine* CurrentLine = GetCurrentLine();
	if (!bHasActiveDialogue || CurrentLine == nullptr || !CurrentLine->Choices.IsValidIndex(ChoiceIndex))
	{
		return false;
	}

	bAwaitingChoice = false;

	const FProjectCRTDialogueChoice& SelectedChoice = CurrentLine->Choices[ChoiceIndex];
	if (SelectedChoice.NextLineIndex == INDEX_NONE)
	{
		EndDialogue();
		return true;
	}

	return MoveToLine(SelectedChoice.NextLineIndex);
}

void UProjectCRTDialogueSubsystem::EndDialogue()
{
	const bool bWasActive = bHasActiveDialogue;

	CurrentDialogueAsset = nullptr;
	CurrentLineIndex = INDEX_NONE;
	bHasActiveDialogue = false;
	bAwaitingChoice = false;

	if (bWasActive)
	{
		OnDialogueEnded.Broadcast();
	}
}

bool UProjectCRTDialogueSubsystem::HasActiveDialogue() const
{
	return bHasActiveDialogue;
}

bool UProjectCRTDialogueSubsystem::IsAwaitingChoice() const
{
	return bAwaitingChoice;
}

int32 UProjectCRTDialogueSubsystem::GetCurrentLineIndex() const
{
	return CurrentLineIndex;
}

UProjectCRTDialogueDataAsset* UProjectCRTDialogueSubsystem::GetCurrentDialogueAsset() const
{
	return CurrentDialogueAsset;
}

bool UProjectCRTDialogueSubsystem::GetCurrentLineData(FProjectCRTDialogueLine& OutDialogueLine) const
{
	const FProjectCRTDialogueLine* CurrentLine = GetCurrentLine();
	if (CurrentLine == nullptr)
	{
		return false;
	}

	OutDialogueLine = *CurrentLine;
	return true;
}

bool UProjectCRTDialogueSubsystem::MoveToLine(int32 NewLineIndex)
{
	if (CurrentDialogueAsset == nullptr || !CurrentDialogueAsset->IsValidLineIndex(NewLineIndex))
	{
		EndDialogue();
		return false;
	}

	CurrentLineIndex = NewLineIndex;
	bAwaitingChoice = false;
	BroadcastCurrentLine();
	return true;
}

void UProjectCRTDialogueSubsystem::BroadcastCurrentLine()
{
	const FProjectCRTDialogueLine* CurrentLine = GetCurrentLine();
	if (CurrentLine == nullptr)
	{
		EndDialogue();
		return;
	}

	bAwaitingChoice = CurrentLine->Choices.Num() > 0;
	OnDialogueLineChanged.Broadcast(*CurrentLine);
}

const FProjectCRTDialogueLine* UProjectCRTDialogueSubsystem::GetCurrentLine() const
{
	if (!bHasActiveDialogue || CurrentDialogueAsset == nullptr)
	{
		return nullptr;
	}

	return CurrentDialogueAsset->GetLine(CurrentLineIndex);
}
'@

$plan = @'
ProjectCRT 언리얼 엔진 교체 작업 문서

작성일: 2026-03-21
문서 목적:
- 언리얼 전환 과정에서 구현 대상 기능과 단계별 진행 현황을 한글로 지속 관리한다.
- 한 번에 처리 가능한 범위를 넘지 않도록, 작은 단계 단위로 작업을 기록한다.

현재 전환 원칙
- 대화 시스템을 우선 포팅한다.
- 심문 시스템은 현재 구조를 그대로 이식하지 않고 추후 재설계한다.
- UI보다 데이터와 런타임 구조를 먼저 고정한다.
- 단계 완료 시 진행률을 갱신한다.

단계 현황
1. 런타임 기반 구조
- 상태: 완료
- 단계 진행도: 100%
- 전체 진행도: 10%
- 결과: GameInstance 및 Story/Dialogue Subsystem 기반을 추가함.

2. 대화 데이터 계층
- 상태: 완료
- 단계 진행도: 100%
- 전체 진행도: 20%
- 결과:
  - 대화 라인 구조체 추가
  - 선택지 구조체 추가
  - 대화 데이터 에셋 클래스 추가
  - 이후 DialogueSubsystem이 참조할 공통 데이터 포맷 준비 완료

3. 대화 세션/진행 제어
- 상태: 완료
- 단계 진행도: 100%
- 전체 진행도: 30%
- 결과:
  - DialogueSubsystem에 현재 대화 세션 상태 추가
  - 대화 시작, 진행, 선택, 종료 API 추가
  - 현재 라인 조회 API 추가
  - 라인 변경/시작/종료 이벤트 추가

다음 단계
4. 대화 UI 프레임워크
- 목표:
  - 텍스트 전용 표시가 가능한 대화 위젯 구조 설계
  - 선택지 위젯과 이벤트 연결
  - 메인 UI와 서브 UI에서 재사용 가능한 UI 타깃 구조 마련

이번 단계 메모
- 대화 세션은 Data Asset에 저장된 라인 인덱스를 기준으로 진행되도록 설계.
- UI가 없어도 대화 진행 테스트가 가능하도록 Subsystem 자체에 상태와 API를 먼저 구현.
- 심문 전용 흐름, 월드 상호작용 연동, 카메라 상태 전환은 아직 포함하지 않음.
'@

$workLog = @'
ProjectCRT 언리얼 전환 작업 로그

날짜: 2026-03-21
작업 단계: 3단계 - 대화 세션/진행 제어

이번 단계 목표
- DialogueSubsystem을 실제 대화 런타임으로 확장한다.
- 대화 시작, 진행, 선택, 종료를 코드 레벨에서 제어할 수 있도록 한다.

이번 단계에서 반영한 내용
- DialogueSubsystem에 현재 대화 에셋 참조 추가
- 현재 라인 인덱스, 활성 여부, 선택 대기 여부 추가
- StartDialogue API 추가
- AdvanceDialogue API 추가
- SelectChoice API 추가
- EndDialogue API 추가
- 현재 라인 조회 API 추가
- 시작/라인변경/종료 이벤트 추가
- 엔진 교체 문서 진행률 갱신

단계 진행도
- 현재 단계 진행도: 100%
- 전체 진행도: 3 / 10 (30%)

다음 단계 예정
- 4단계: 대화 UI 프레임워크
- UMG 위젯과 DialogueSubsystem 이벤트를 연결하는 구조를 만들 예정
'@

$featureList = @'
ProjectCRT 언리얼 기능 정의 및 작업 단계표

작성일: 2026-03-21
원본 프로젝트: C:\Users\user\Documents\GitHub\ProjectCRT
대상 프로젝트: C:\Users\user\Documents\Unreal Projects\ProjectCRT_Unreal
기준 엔진: Unreal Engine 5.2

작업 원칙
- 기능 단위로 정의하고, 의존성이 낮은 순서부터 순차 구현한다.
- 심문 시스템은 추후 전면 개편 예정이므로 초기 포팅 대상에서 제외한다.
- 각 단계 완료 시 해당 단계 진행도와 전체 진행도를 함께 갱신한다.
- 문서는 가능한 한 한글 기준으로 유지한다.

기능 목록 및 권장 순서

[1] 런타임 기반 구조
정의:
- GameInstance, Subsystem, 공통 초기화 구조를 확정한다.
- Unity의 싱글톤 의존을 Unreal의 장기 수명 객체 구조로 치환하기 위한 기반이다.
주요 결과물:
- UProjectCRTGameInstance
- UProjectCRTDialogueSubsystem
- UProjectCRTStorySubsystem
상태: 완료
단계 진행도: 100%
전체 진행도: 1 / 10 (10%)

[2] 대화 데이터 계층
정의:
- Unity의 DialogueObject, ScriptableObject 기반 데이터를 Unreal Data Asset + Struct로 치환한다.
- UI와 진행 로직보다 먼저 데이터 포맷을 고정한다.
주요 결과물:
- 대화 라인 구조체
- 선택지 구조체
- 대화 데이터 에셋 클래스
상태: 완료
단계 진행도: 100%
전체 진행도: 2 / 10 (20%)

[3] 대화 세션/진행 제어
정의:
- 대화 시작, 라인 진행, 선택지 표시, 종료, 상태 복귀를 하나의 세션 흐름으로 통합한다.
- Unity DialogueManager의 핵심 역할을 Unreal Subsystem으로 옮긴다.
주요 결과물:
- DialogueSubsystem 세션 상태
- Start/Advance/End API
- 세션 이벤트 델리게이트
상태: 완료
단계 진행도: 100%
전체 진행도: 3 / 10 (30%)

[4] 대화 UI 프레임워크
정의:
- 메인 UI와 서브 UI 모두에서 재사용 가능한 UMG 대화 위젯 구조를 만든다.
- 일부 UI 요소가 없어도 텍스트만으로 동작 가능해야 한다.
주요 결과물:
- 대화 표시 위젯
- 선택지 위젯
- UI 타깃 전환 규칙
상태: 다음 작업 대상

[5] 상호작용 시스템
정의:
- NPC, 오브젝트, 모니터 등과의 상호작용 진입점을 통합한다.
- Unity MainLoop + Raycast 기반 상호작용을 Unreal Trace/Interface 방식으로 재구성한다.
주요 결과물:
- 상호작용 인터페이스
- 플레이어 Trace 로직
- 상호작용 상태 제어
상태: 대기

[6] 플레이어/카메라/입력 기반
정의:
- 플레이어 조작, 카메라 전환, 입력 매핑을 Unreal 방식으로 정리한다.
- 대화/터미널/상호작용 상태에 따라 입력 제어가 가능해야 한다.
주요 결과물:
- Character 또는 Pawn 기반 클래스
- PlayerController 입력 제어
- Enhanced Input 매핑
상태: 대기

[7] 스토리/플래그/진행 상태
정의:
- Unity의 전역 상태와 이벤트성 진행 값을 StorySubsystem 중심으로 이관한다.
- 대화와 단서 획득 조건이 참조하는 런타임 상태 저장소 역할을 맡는다.
주요 결과물:
- 플래그 저장 구조
- 챕터/단서/분기 상태 API
상태: 대기

[8] 씬 전환 및 초기화 흐름
정의:
- 씬 진입 시 필요한 초기화, 상태 복구, 플레이어 배치를 정리한다.
- Unity SceneInitializer의 역할을 Unreal 레벨 로드 흐름에 맞게 재설계한다.
주요 결과물:
- 레벨 초기화 규칙
- 공통 부트스트랩 흐름
상태: 대기

[9] 터미널/컴퓨터 UI 시스템
정의:
- 모니터 UI, 터미널 입력, 스크롤/출력 갱신 구조를 UMG 기준으로 재설계한다.
- 현재 Unity terminalManager/MonitorUIRaycaster 역할을 분리 이관한다.
주요 결과물:
- 터미널 위젯
- 입력 처리 API
- UI 상호작용 모드 전환
상태: 대기

[10] 단서/증거 시스템
정의:
- 단서 획득, 중복 체크, 대화 연계, 스토리 반영 구조를 정리한다.
- 추후 심문 시스템 재설계와도 자연스럽게 연결될 수 있어야 한다.
주요 결과물:
- 단서 데이터 구조
- 획득/조회 API
- 대화/스토리 연동 포인트
상태: 대기

현재 작업 대상
- 4단계: 대화 UI 프레임워크
- 목표: DialogueSubsystem 이벤트를 UMG 위젯 계층과 연결할 수 있는 텍스트 중심 UI 기반을 마련한다.
'@

Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\Core\ProjectCRTDialogueSubsystem.h') -Value $subsystemH -Encoding UTF8
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\Core\ProjectCRTDialogueSubsystem.cpp') -Value $subsystemCpp -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Migration_Plan.md') -Value $plan -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Migration_Work_Update_2026-03-21.txt') -Value $workLog -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Implementation_Feature_List_KR.txt') -Value $featureList -Encoding UTF8
