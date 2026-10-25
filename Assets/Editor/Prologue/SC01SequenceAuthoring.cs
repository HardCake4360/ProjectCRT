using System.Collections.Generic;
using ProjectCRT.Cutscenes;

namespace ProjectCRT.Prologue.Editor
{
    public static partial class SC01CutsceneAuthoring
    {
        static List<CutsceneTurn> CreateTurns()
        {
            var turns=new List<CutsceneTurn>();
            CutsceneActorCue Act(string actor,string clip,bool loop=true,string look=null)=>new CutsceneActorCue{actorId=actor,clip=clips[clip],loop=loop,changeLookAt=true,lookAtId=look??""};
            CutsceneCameraCue Cam(int index,CutsceneCameraMode mode=CutsceneCameraMode.Cut,float duration=.65f)=>new CutsceneCameraCue{mode=mode,shotId=stage.shots[index].id,duration=duration};
            CutsceneTurn Silent(string id,string label,float duration,CutsceneCameraCue camera,params CutsceneActorCue[] actions)
            {var t=new CutsceneTurn{id=id,label=label,hasDialogue=false,advance=CutsceneAdvanceMode.Auto,minimumDuration=duration,autoDelay=0,camera=camera,actors=new List<CutsceneActorCue>(actions)};turns.Add(t);return t;}
            CutsceneTurn Line(string id,string who,string text,int camera=-1,string clip=null,string look=null)
            {
                var t=new CutsceneTurn{id=id,label=(who=="san"?"산":"기사")+" · "+text,speakerId=who,text=text,charsPerSecond=24,punctuationPause=.2f,minimumDuration=.5f,camera=camera>=0?Cam(camera):new CutsceneCameraCue(),advance=CutsceneAdvanceMode.Manual};
                if(clip!=null)t.actors.Add(Act(who,clip,true,look));turns.Add(t);return t;
            }
            Silent("sc01-establish","CAM01 · 비 오는 도시와 택시",5,Cam(0),Act("taxi_road","TaxiEstablish",false),Act("san","SanSeatB",true,"window_right"),Act("driver","DriverDriveB"),Act("wipers","Wipers"),Act("scenery","SetA"));
            Silent("sc01-cabin","CAM02 · 택시 실내와 와이퍼",1.4f,Cam(1),Act("scenery","SetB"));
            Line("sc01-d01","driver","그나저나 손님, 흔치 않군요.",clip:"DriverTalkB");
            Silent("sc01-pause","기사의 말 뒤 짧은 공백",.8f,new CutsceneCameraCue());
            Line("sc01-d02","driver","요즘엔 보통 여기서 나가려는 손님들뿐이라서 말이죠.",clip:"DriverTalkB");
            Line("sc01-d03","san","저야 가고 싶어서 가는 건 아닙니다만. 일 때문에 어쩔 수 없죠.",2,"SanTalkB","driver_face");
            Line("sc01-d04","driver","일이요? 이 동네에?",clip:"DriverMirror",look:"mirror_rear");
            Line("sc01-d05","driver","무슨 일 하시는데요?",clip:"DriverMirror",look:"mirror_rear");
            Line("sc01-d06","san","형사입니다. 이번에 LSPD로 전출 왔습니다.",clip:"SanTalkB",look:"driver_face");
            Line("sc01-d07","driver","경찰이셨군요.",clip:"DriverTalkB");
            Line("sc01-d08","driver","그럼 일거리는 걱정 없으시겠네요.",clip:"DriverTalkB");
            Line("sc01-d09","san","……그렇게 심각합니까?",clip:"SanTalkB",look:"driver_face");
            Line("sc01-d10","driver","하하. 괜한 소릴 했군요. 처음 오시는 분한테.",clip:"DriverTalkB");
            Line("sc01-d11","driver","뭐, 어디나 사람 사는 곳은 다 비슷하지 않겠습니까.",clip:"DriverTalkB");
            Line("sc01-d12","san","……그렇겠죠.",clip:"SanNodB",look:"window_right");
            Silent("sc01-cd","CAM04 · 신호 대기, CD 삽입",4.8f,Cam(3,CutsceneCameraMode.Blend,.6f),Act("driver","DriverCD",false),Act("disc","DiscInsert",false),Act("san","SanSeatB",true,"window_right"));
            Line("sc01-d13","driver","음악 좀 틀어도 괜찮겠습니까?",clip:"DriverTalkB");
            Line("sc01-d14","san","네. 괜찮습니다.",clip:"SanNodB",look:"driver_face");
            Silent("sc01-volume","기사의 볼륨 조절",1.5f,new CutsceneCameraCue(),Act("driver","DriverVolume",false));
            Line("sc01-d15","driver","이 시간엔 이런 게 좋더라고요.",clip:"DriverTalkB");
            Silent("sc01-listen","CAM05 · 음악을 듣고 창밖을 보는 산",2.2f,Cam(4),Act("san","SanSeatB",true,"window_right"),Act("driver","DriverDriveB"));
            Line("sc01-d16","driver","그런데 손님, 여기엔 아는 사람이라도 있으십니까?",clip:"DriverTalkB");
            Line("sc01-d17","san","네. 오래된 친구가 한 명 있습니다.",clip:"SanTalkB",look:"driver_face");
            Line("sc01-d18","driver","그거 다행이네요.",clip:"DriverTalkB");
            Line("sc01-d19","san","왜요?",clip:"SanTalkB",look:"driver_face");
            Line("sc01-d20","driver","아는 사람 하나 없는 동네에서 새로 시작하는 것만큼 외로운 일도 없으니까요.",clip:"DriverTalkB");
            Line("sc01-d21","san","……그렇군요.",clip:"SanNodB",look:"window_right");
            Silent("sc01-city","CAM06 · 멀어지는 택시를 따라 도시 진입",6,new CutsceneCameraCue{mode=CutsceneCameraMode.Path,pathId="city_follow",duration=6,joinDuration=0},Act("taxi_road","TaxiCity",false),Act("scenery","SetA"));
            Silent("sc01-timepassage","시간 경과 · 암전",1.2f,new CutsceneCameraCue());
            Silent("sc01-arrival","CAM07 · 주거 건물 앞에 정차",3,Cam(6),Act("taxi_arrival","TaxiArrive",false),Act("san","SanArrive",false),Act("driver","DriverArrive",false),Act("scenery","SetC"));
            Line("sc01-d22","driver","도착했습니다.",clip:"DriverTalkC");
            Line("sc01-d23","san","감사합니다.",clip:"SanTalkC");
            Silent("sc01-fare","산이 요금을 지불한다",1.5f,new CutsceneCameraCue(),Act("san","SanPay",false));
            Line("sc01-d24","driver","그럼 로스 시뮬라에서도 잘해보세요, 형사님.",clip:"DriverTalkC");
            Line("sc01-d25","san","네. 감사합니다.",clip:"SanTalkC");
            Silent("sc01-exit","문을 열고 택시에서 내린다",4.2f,new CutsceneCameraCue(),Act("taxi_arrival","TaxiDoorOpen",false),Act("san","SanExit",false));
            Silent("sc01-lookcity","CAM08 · 택시 출발, 도시를 둘러보는 산",2.8f,Cam(7,CutsceneCameraMode.Blend,.7f),Act("taxi_arrival","TaxiLeave",false),Act("driver","DriverLeave",false),Act("san","SanLookCity",false));
            Silent("sc01-pickup","산이 짐을 든다",2.8f,new CutsceneCameraCue(),Act("san","SanPickup",false),Act("bag","BagPickup",false));
            Silent("sc01-entry","짐을 들고 건물 입구로 향한다",7,new CutsceneCameraCue{mode=CutsceneCameraMode.Path,pathId="entry_follow",duration=7,joinDuration=.25f},Act("san","SanWalkEntry",false),Act("bag","BagWalk",false));
            return turns;
        }
    }
}
