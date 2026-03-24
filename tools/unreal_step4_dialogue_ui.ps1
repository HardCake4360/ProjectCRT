$root = 'C:\Users\user\Documents\Unreal Projects\ProjectCRT_Unreal'

$buildCs = @'
// Copyright Epic Games, Inc. All Rights Reserved.

using UnrealBuildTool;

public class ProjectCRT_Unreal : ModuleRules
{
	public ProjectCRT_Unreal(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
	
		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "InputCore", "UMG" });

		PrivateDependencyModuleNames.AddRange(new string[] { "Slate", "SlateCore" });

		// Uncomment if you are using online features
		// PrivateDependencyModuleNames.Add("OnlineSubsystem");

		// To include OnlineSubsystemSteam, add it to the plugins section in your uproject file with the Enabled attribute set to true
	}
}
'@

$choiceWidgetH = @'
#pragma once

#include "CoreMinimal.h"
#include "Dialogue/ProjectCRTDialogueTypes.h"
#include "Blueprint/UserWidget.h"
#include "ProjectCRTDialogueChoiceWidgetBase.generated.h"

class UButton;
class UTextBlock;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FProjectCRTChoiceSelectedSignature, int32, ChoiceIndex);

UCLASS(Abstract, BlueprintType, Blueprintable)
class PROJECTCRT_UNREAL_API UProjectCRTDialogueChoiceWidgetBase : public UUserWidget
{
	GENERATED_BODY()

public:
	virtual void NativeConstruct() override;

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	void SetupChoice(int32 InChoiceIndex, const FProjectCRTDialogueChoice& InChoiceData);

	UPROPERTY(BlueprintAssignable, Category = "Dialogue")
	FProjectCRTChoiceSelectedSignature OnChoiceSelected;

protected:
	UFUNCTION()
	void HandleChoiceButtonClicked();

	UPROPERTY(meta = (BindWidgetOptional), BlueprintReadOnly)
	TObjectPtr<UButton> ChoiceButton = nullptr;

	UPROPERTY(meta = (BindWidgetOptional), BlueprintReadOnly)
	TObjectPtr<UTextBlock> ChoiceTextBlock = nullptr;

	UPROPERTY(BlueprintReadOnly, Category = "Dialogue")
	FProjectCRTDialogueChoice ChoiceData;

	UPROPERTY(BlueprintReadOnly, Category = "Dialogue")
	int32 ChoiceIndex = INDEX_NONE;
};
'@

$choiceWidgetCpp = @'
#include "UI/ProjectCRTDialogueChoiceWidgetBase.h"

#include "Components/Button.h"
#include "Components/TextBlock.h"

void UProjectCRTDialogueChoiceWidgetBase::NativeConstruct()
{
	Super::NativeConstruct();

	if (ChoiceButton != nullptr)
	{
		ChoiceButton->OnClicked.RemoveDynamic(this, &UProjectCRTDialogueChoiceWidgetBase::HandleChoiceButtonClicked);
		ChoiceButton->OnClicked.AddDynamic(this, &UProjectCRTDialogueChoiceWidgetBase::HandleChoiceButtonClicked);
	}
}

void UProjectCRTDialogueChoiceWidgetBase::SetupChoice(int32 InChoiceIndex, const FProjectCRTDialogueChoice& InChoiceData)
{
	ChoiceIndex = InChoiceIndex;
	ChoiceData = InChoiceData;

	if (ChoiceTextBlock != nullptr)
	{
		ChoiceTextBlock->SetText(ChoiceData.ChoiceText);
	}
}

void UProjectCRTDialogueChoiceWidgetBase::HandleChoiceButtonClicked()
{
	if (ChoiceIndex != INDEX_NONE)
	{
		OnChoiceSelected.Broadcast(ChoiceIndex);
	}
}
'@

$dialogueWidgetH = @'
#pragma once

#include "CoreMinimal.h"
#include "Dialogue/ProjectCRTDialogueTypes.h"
#include "Blueprint/UserWidget.h"
#include "ProjectCRTDialogueWidgetBase.generated.h"

class UProjectCRTDialogueChoiceWidgetBase;
class UProjectCRTDialogueSubsystem;
class UPanelWidget;
class UTextBlock;

UCLASS(Abstract, BlueprintType, Blueprintable)
class PROJECTCRT_UNREAL_API UProjectCRTDialogueWidgetBase : public UUserWidget
{
	GENERATED_BODY()

public:
	virtual void NativeConstruct() override;
	virtual void NativeDestruct() override;

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	void ConnectToDialogueSubsystem(UProjectCRTDialogueSubsystem* InDialogueSubsystem);

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	void RefreshFromCurrentState();

	UFUNCTION(BlueprintCallable, Category = "Dialogue")
	void RequestAdvanceDialogue();

protected:
	UFUNCTION()
	void HandleDialogueStarted(UProjectCRTDialogueDataAsset* InDialogueAsset);

	UFUNCTION()
	void HandleDialogueLineChanged(FProjectCRTDialogueLine InDialogueLine);

	UFUNCTION()
	void HandleDialogueEnded();

	UFUNCTION()
	void HandleChoiceSelected(int32 SelectedChoiceIndex);

	void RebuildChoices(const TArray<FProjectCRTDialogueChoice>& Choices);
	void ClearChoices();

	UPROPERTY(meta = (BindWidgetOptional), BlueprintReadOnly)
	TObjectPtr<UTextBlock> SpeakerNameText = nullptr;

	UPROPERTY(meta = (BindWidgetOptional), BlueprintReadOnly)
	TObjectPtr<UTextBlock> BodyText = nullptr;

	UPROPERTY(meta = (BindWidgetOptional), BlueprintReadOnly)
	TObjectPtr<UPanelWidget> ChoiceContainer = nullptr;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Dialogue")
	TSubclassOf<UProjectCRTDialogueChoiceWidgetBase> ChoiceWidgetClass;

	UPROPERTY(BlueprintReadOnly, Category = "Dialogue")
	TObjectPtr<UProjectCRTDialogueSubsystem> DialogueSubsystem = nullptr;

	UPROPERTY(BlueprintReadOnly, Category = "Dialogue")
	FProjectCRTDialogueLine CurrentLineData;
};
'@

$dialogueWidgetCpp = @'
#include "UI/ProjectCRTDialogueWidgetBase.h"

#include "Core/ProjectCRTDialogueSubsystem.h"
#include "UI/ProjectCRTDialogueChoiceWidgetBase.h"
#include "Components/PanelWidget.h"
#include "Components/TextBlock.h"
#include "Engine/GameInstance.h"

void UProjectCRTDialogueWidgetBase::NativeConstruct()
{
	Super::NativeConstruct();

	if (DialogueSubsystem == nullptr)
	{
		if (UGameInstance* GameInstance = GetGameInstance())
		{
			ConnectToDialogueSubsystem(GameInstance->GetSubsystem<UProjectCRTDialogueSubsystem>());
		}
	}

	RefreshFromCurrentState();
}

void UProjectCRTDialogueWidgetBase::NativeDestruct()
{
	if (DialogueSubsystem != nullptr)
	{
		DialogueSubsystem->OnDialogueStarted.RemoveAll(this);
		DialogueSubsystem->OnDialogueLineChanged.RemoveAll(this);
		DialogueSubsystem->OnDialogueEnded.RemoveAll(this);
	}

	Super::NativeDestruct();
}

void UProjectCRTDialogueWidgetBase::ConnectToDialogueSubsystem(UProjectCRTDialogueSubsystem* InDialogueSubsystem)
{
	if (DialogueSubsystem == InDialogueSubsystem)
	{
		return;
	}

	if (DialogueSubsystem != nullptr)
	{
		DialogueSubsystem->OnDialogueStarted.RemoveAll(this);
		DialogueSubsystem->OnDialogueLineChanged.RemoveAll(this);
		DialogueSubsystem->OnDialogueEnded.RemoveAll(this);
	}

	DialogueSubsystem = InDialogueSubsystem;

	if (DialogueSubsystem != nullptr)
	{
		DialogueSubsystem->OnDialogueStarted.AddDynamic(this, &UProjectCRTDialogueWidgetBase::HandleDialogueStarted);
		DialogueSubsystem->OnDialogueLineChanged.AddDynamic(this, &UProjectCRTDialogueWidgetBase::HandleDialogueLineChanged);
		DialogueSubsystem->OnDialogueEnded.AddDynamic(this, &UProjectCRTDialogueWidgetBase::HandleDialogueEnded);
	}
}

void UProjectCRTDialogueWidgetBase::RefreshFromCurrentState()
{
	if (DialogueSubsystem == nullptr || !DialogueSubsystem->HasActiveDialogue())
	{
		if (SpeakerNameText != nullptr)
		{
			SpeakerNameText->SetText(FText::GetEmpty());
		}

		if (BodyText != nullptr)
		{
			BodyText->SetText(FText::GetEmpty());
		}

		ClearChoices();
		return;
	}

	FProjectCRTDialogueLine DialogueLine;
	if (DialogueSubsystem->GetCurrentLineData(DialogueLine))
	{
		HandleDialogueLineChanged(DialogueLine);
	}
}

void UProjectCRTDialogueWidgetBase::RequestAdvanceDialogue()
{
	if (DialogueSubsystem != nullptr)
	{
		DialogueSubsystem->AdvanceDialogue();
	}
}

void UProjectCRTDialogueWidgetBase::HandleDialogueStarted(UProjectCRTDialogueDataAsset* InDialogueAsset)
{
	RefreshFromCurrentState();
}

void UProjectCRTDialogueWidgetBase::HandleDialogueLineChanged(FProjectCRTDialogueLine InDialogueLine)
{
	CurrentLineData = InDialogueLine;

	if (SpeakerNameText != nullptr)
	{
		SpeakerNameText->SetText(CurrentLineData.SpeakerName);
	}

	if (BodyText != nullptr)
	{
		BodyText->SetText(CurrentLineData.BodyText);
	}

	RebuildChoices(CurrentLineData.Choices);
}

void UProjectCRTDialogueWidgetBase::HandleDialogueEnded()
{
	if (SpeakerNameText != nullptr)
	{
		SpeakerNameText->SetText(FText::GetEmpty());
	}

	if (BodyText != nullptr)
	{
		BodyText->SetText(FText::GetEmpty());
	}

	CurrentLineData = FProjectCRTDialogueLine();
	ClearChoices();
}

void UProjectCRTDialogueWidgetBase::HandleChoiceSelected(int32 SelectedChoiceIndex)
{
	if (DialogueSubsystem != nullptr)
	{
		DialogueSubsystem->SelectChoice(SelectedChoiceIndex);
	}
}

void UProjectCRTDialogueWidgetBase::RebuildChoices(const TArray<FProjectCRTDialogueChoice>& Choices)
{
	ClearChoices();

	if (ChoiceContainer == nullptr || ChoiceWidgetClass == nullptr)
	{
		return;
	}

	for (int32 ChoiceIndex = 0; ChoiceIndex < Choices.Num(); ++ChoiceIndex)
	{
		UProjectCRTDialogueChoiceWidgetBase* ChoiceWidget = CreateWidget<UProjectCRTDialogueChoiceWidgetBase>(this, ChoiceWidgetClass);
		if (ChoiceWidget == nullptr)
		{
			continue;
		}

		ChoiceWidget->SetupChoice(ChoiceIndex, Choices[ChoiceIndex]);
		ChoiceWidget->OnChoiceSelected.AddDynamic(this, &UProjectCRTDialogueWidgetBase::HandleChoiceSelected);
		ChoiceContainer->AddChild(ChoiceWidget);
	}
}

void UProjectCRTDialogueWidgetBase::ClearChoices()
{
	if (ChoiceContainer != nullptr)
	{
		ChoiceContainer->ClearChildren();
	}
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

4. 대화 UI 프레임워크
- 상태: 완료
- 단계 진행도: 100%
- 전체 진행도: 40%
- 결과:
  - 텍스트 전용 동작이 가능한 대화 위젯 베이스 추가
  - 선택지 위젯 베이스 추가
  - DialogueSubsystem 이벤트와 UMG 위젯 연결
  - Speaker/Body/Choice 영역이 일부 비어 있어도 동작 가능한 구조 마련

다음 단계
5. 상호작용 시스템
- 목표:
  - 월드 오브젝트 상호작용 인터페이스 정의
  - 플레이어 Trace 기반 상호작용 진입점 마련
  - 대화 시스템과 상호작용 진입 연결

이번 단계 메모
- 위젯은 Blueprint에서 확장할 수 있는 베이스 클래스로 추가.
- 텍스트 블록과 선택지 컨테이너는 선택적 바인딩으로 설계하여, 메인 UI와 서브 UI가 같은 클래스 구조를 공유할 수 있도록 준비.
- 심문용 UI 타깃 전환은 추후 별도 재설계 범위로 남겨둠.
'@

$workLog = @'
ProjectCRT 언리얼 전환 작업 로그

날짜: 2026-03-21
작업 단계: 4단계 - 대화 UI 프레임워크

이번 단계 목표
- DialogueSubsystem과 연결되는 UMG 기반 대화 위젯 베이스를 만든다.
- 텍스트만 있어도 동작 가능한 UI 구조를 우선 확보한다.

이번 단계에서 반영한 내용
- UMG 모듈 의존성 추가
- 대화 위젯 베이스 클래스 추가
- 선택지 위젯 베이스 클래스 추가
- DialogueSubsystem 이벤트 구독 및 해제 로직 추가
- 현재 라인 반영, 선택지 생성, 대화 종료 시 UI 초기화 처리 추가

단계 진행도
- 현재 단계 진행도: 100%
- 전체 진행도: 4 / 10 (40%)

다음 단계 예정
- 5단계: 상호작용 시스템
- 월드에서 대화를 시작할 수 있는 상호작용 인터페이스와 플레이어 진입점을 만들 예정
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
상태: 완료
단계 진행도: 100%
전체 진행도: 4 / 10 (40%)

[5] 상호작용 시스템
정의:
- NPC, 오브젝트, 모니터 등과의 상호작용 진입점을 통합한다.
- Unity MainLoop + Raycast 기반 상호작용을 Unreal Trace/Interface 방식으로 재구성한다.
주요 결과물:
- 상호작용 인터페이스
- 플레이어 Trace 로직
- 상호작용 상태 제어
상태: 다음 작업 대상

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
- 5단계: 상호작용 시스템
- 목표: 월드 상호작용 인터페이스와 플레이어 진입점을 만들어 대화 시작 흐름을 연결한다.
'@

New-Item -ItemType Directory -Force -Path (Join-Path $root 'Source\ProjectCRT_Unreal\UI') | Out-Null
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\ProjectCRT_Unreal.Build.cs') -Value $buildCs -Encoding UTF8
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\UI\ProjectCRTDialogueChoiceWidgetBase.h') -Value $choiceWidgetH -Encoding UTF8
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\UI\ProjectCRTDialogueChoiceWidgetBase.cpp') -Value $choiceWidgetCpp -Encoding UTF8
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\UI\ProjectCRTDialogueWidgetBase.h') -Value $dialogueWidgetH -Encoding UTF8
Set-Content -Path (Join-Path $root 'Source\ProjectCRT_Unreal\UI\ProjectCRTDialogueWidgetBase.cpp') -Value $dialogueWidgetCpp -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Migration_Plan.md') -Value $plan -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Migration_Work_Update_2026-03-21.txt') -Value $workLog -Encoding UTF8
Set-Content -Path (Join-Path $root 'Unreal_Implementation_Feature_List_KR.txt') -Value $featureList -Encoding UTF8
