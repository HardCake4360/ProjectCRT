using System.IO;
using System.Collections.Generic;
using ProjectCRT.Cutscenes;
namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC23CutsceneAuthoring
    {
        static List<CutsceneTurn> BarTurns()
        {
            var turns=new List<CutsceneTurn>();
            Beat(turns,"sc03-enter","CAM01 · 문을 열고 입장, 가게를 둘러보기",4.5f,Cam("sc03-entrance"),Act("san","SanBarEntry",false),Act("jeremy","JeremyCounterIdle"),Act("rebecca","RebeccaHidden"),Act("room02_door","RoomDoorAjar"),Act("rebecca_bottle","BottleHidden"),Act("later_drink","DrinkBefore"),Act("entrance_door","EntranceDoorEnter",false),Act("service_door","ServiceDoorClosed"));
            Beat(turns,"sc03-counter-arrive","CAM02 · 제레미가 산을 발견한다",.85f,Cam("sc03-greeting",CutsceneCameraMode.Blend,.65f),Act("san","SanBarStand",true,"jeremy"));
            var source=UnityEngine.JsonUtility.FromJson<DialogueSource>(File.ReadAllText(Folder+"/SC03_DialogueSource.json"));int previousCamera=2;bool jeremyLeft=false;
            for(int i=0;i<source.lines.Length;i++){
                var line=source.lines[i];
                if(line.camera!=previousCamera){
                    switch(line.camera){
                        case 3:Beat(turns,"sc03-seat","CAM03 · 제레미의 안내에 따라 자리에 앉는다",2.4f,Cam("sc03-two-jeremy",CutsceneCameraMode.Blend,.8f),Act("san","SanBarSit",false,"jeremy"),Act("jeremy","JeremyPoint",false,"san"));break;
                        case 4:Beat(turns,"sc03-rebecca-enter","CAM04 · 병을 들고 나온 레베카",3.6f,Cam("sc03-rebecca-enter"),Act("rebecca","RebeccaEnter",false,"san"),Act("service_door","ServiceDoorEnter",false),Act("rebecca_bottle","BottleCarry",false),Act("san","SanBarSeat",true,"rebecca"));break;
                        case 7:
                            Beat(turns,"sc03-pullback","잔을 꺼내고 카메라가 천천히 뒤로 빠진다",3.2f,Cam("sc03-later-wide",CutsceneCameraMode.Blend,3.2f),Act("rebecca","RebeccaGlass",false,"san"),Act("san","SanBarSeat",true,"rebecca"));
                            Beat(turns,"sc03-timepassage","FADE OUT · 시간 경과",1.45f);
                            Beat(turns,"sc03-later","CAM07 · 조금 비워진 술잔과 안주",1.15f,Cam("sc03-later-two"),Act("rebecca","RebeccaLean",true,"san"),Act("later_drink","DrinkLater"),Act("jeremy","JeremyHidden"));break;
                        case 8:Beat(turns,"sc03-thud","SFX · 프라이빗 홀에서 들려오는 쿵 소리",1.2f,Cam("sc03-noise"),Act("rebecca","RebeccaNoise",false),Act("san","SanBarLook",false),Act("jeremy","JeremyFallen"));break;
                        case 9:Beat(turns,"sc03-leave-ready","CAM09 · 카운터에서 나올 준비",.8f,Cam("sc03-leave",CutsceneCameraMode.Blend,.7f));break;
                        case 10:
                            Beat(turns,"sc03-leave-counter","카운터를 나와 프라이빗 홀로 이동",9,Cam("sc03-leave"),Act("rebecca","RebeccaLeaveCounter",false),Act("san","SanFollowCounter",false));
                            Beat(turns,"sc03-hall-walk","CAM10 · 복도로 들어가는 두 사람을 뒤에서 따라간다",5.5f,new CutsceneCameraCue{mode=CutsceneCameraMode.Path,pathId="sc03-private-hall",duration=5.5f,joinDuration=.45f},Act("rebecca","RebeccaHallWalk",false),Act("san","SanHallWalk",false));break;
                        case 12:
                            Beat(turns,"sc03-no-answer-final","문 안쪽에서는 아무 대답도 들리지 않는다",1.1f,Cam("sc03-threshold"),Act("rebecca","RebeccaThreshold"),Act("san","SanThreshold"));
                            Beat(turns,"sc03-door-open","CAM11 · 레베카가 조심스럽게 문을 민다",2.8f,Cam("sc03-over-shoulder",CutsceneCameraMode.Blend,.8f),Act("rebecca","RebeccaPushDoor",false),Act("room02_door","RoomDoorOpen",false));
                            Beat(turns,"sc03-discovery","쓰러진 제레미를 발견한 뒤 몇 초간의 정적",3.5f,null,Act("rebecca","RebeccaThreshold",true,"jeremy"));
                            Beat(turns,"sc03-shock-reaction","CAM12 · 레베카의 굳어지는 얼굴",1.8f,Cam("sc03-shock",CutsceneCameraMode.Blend,.55f),Act("rebecca","RebeccaShock",false,"jeremy"));break;
                    }
                    previousCamera=line.camera;
                }
                string camera=line.camera==2?"sc03-greeting":line.camera==3?"sc03-two-jeremy":line.camera==4?"sc03-rebecca-enter":line.camera==5?(line.speaker=="san"?"sc03-san-close":"sc03-rebecca-close"):line.camera==6?"sc03-three":line.camera==7?(i%4==0?line.speaker=="san"?"sc03-san-close":"sc03-rebecca-close":"sc03-later-two"):line.camera==8?"sc03-noise":line.camera==9?"sc03-leave":line.camera==10?"sc03-threshold":"sc03-shock";
                string target=line.speaker=="san"?(line.camera<=3?"jeremy":"rebecca"):"san";
                string clip=line.speaker=="san"?(line.camera==2?"SanBarTalkStand":line.camera>=9?"SanThreshold":"SanBarTalk"):line.speaker=="jeremy"?"JeremyCounterTalk":line.camera==7?"RebeccaLeanTalk":line.camera==8?"RebeccaNoise":line.camera>=10?"RebeccaThreshold":"RebeccaCounterTalk";
                var turn=new CutsceneTurn{id="sc03-d"+(i+1).ToString("00"),label="CAM"+line.camera.ToString("00")+" · "+line.text,speakerId=line.speaker,text=line.text,charsPerSecond=24,punctuationPause=.2f,minimumDuration=.45f,camera=Cam(camera),actors=new List<CutsceneActorCue>{Act(line.speaker,clip,true,line.camera>=8?null:target)}};
                // Keep listener poses alive and release the previous speaker's talk motion.
                if(line.camera<=7){if(line.speaker!="san")turn.actors.Add(Act("san",line.camera==2?"SanBarStand":"SanBarSeat",true,line.camera<=3?"jeremy":"rebecca"));if(line.speaker!="rebecca"&&line.camera>=5)turn.actors.Add(Act("rebecca",line.camera==7?"RebeccaLean":"RebeccaCounterIdle",true,"san"));if(line.speaker!="jeremy"&&line.camera<=6&&!jeremyLeft)turn.actors.Add(Act("jeremy","JeremyCounterIdle",true,"san"));}
                if(line.text=="미리 연락이라도 하지 그랬어."){turn.actors.Clear();turn.actors.Add(Act("rebecca","RebeccaPlaceBottle",false,"san"));turn.actors.Add(Act("rebecca_bottle","BottlePlace",false));}
                if(line.text=="아니, 아직 괜찮아.")turn.actors.Add(new CutsceneActorCue{actorId="san",clip=clips["SanBarLook"],loop=false,timing=CutsceneCueTiming.TextComplete});
                if(line.camera==9&&line.speaker=="san"){turn.actors.Clear();turn.actors.Add(Act("san","SanBarRise",false));turn.waitForActors=true;}
                if(line.camera==9&&line.speaker=="rebecca")turn.actors.Clear();
                if(line.camera==12){turn.actors.Clear();turn.actors.Add(Act("rebecca","RebeccaShock",false,"jeremy"));}
                turns.Add(turn);
                if(line.text=="네, 점장님. 그럼 두 분 편히 이야기 나누세요."){
                    Beat(turns,"sc03-jeremy-bow","제레미가 고개를 숙인다",1.4f,null,Act("jeremy","JeremyBow",false));
                    // His continuous departure can finish while the two friends talk.
                    Beat(turns,"sc03-jeremy-leave","제레미가 홀 쪽으로 이동한다",3,null,Act("jeremy","JeremyLeave",false));
                    jeremyLeft=true;
                }
                if(line.camera==8&&(line.text=="제레미?"||line.text=="제레미! 안에 있어?"))Beat(turns,"sc03-silence-"+i,"대답 없는 복도",1.7f);
            }
            Beat(turns,"sc03-black","CUT TO BLACK · SC03 종료",.35f);return turns;
        }
    }
}
