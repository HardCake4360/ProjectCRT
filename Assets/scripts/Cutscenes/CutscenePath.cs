using UnityEngine;

namespace ProjectCRT.Cutscenes
{
    /// <summary>Scene-authored Catmull-Rom curve. Move child knots to edit the camera route.</summary>
    public sealed class CutscenePath : MonoBehaviour
    {
        public string id="path";
        public Transform[] knots=new Transform[0];
        [Range(15,100)] public float fieldOfView=42;
        public Transform lookAt;
        public Vector3 lookAtOffset;
        public Vector2 framingOffset;
        readonly float[] arcLengths=new float[65];
        Vector3[] cachedKnots;
        public Vector3 SamplePosition(float t)
        {
            if(knots==null || knots.Length==0)return transform.position;
            if(knots.Length==1)return knots[0]!=null?knots[0].position:transform.position;
            float raw=Mathf.Clamp01(t)*(knots.Length-1);int segment=Mathf.Min(Mathf.FloorToInt(raw),knots.Length-2);float u=raw-segment;
            Vector3 p0=Point(segment-1),p1=Point(segment),p2=Point(segment+1),p3=Point(segment+2);
            return 0.5f*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u*u+(-p0+3*p1-3*p2+p3)*u*u*u);
        }
        Vector3 Point(int i){var knot=knots[Mathf.Clamp(i,0,knots.Length-1)];return knot!=null?knot.position:transform.position;}
        public float DistanceToParameter(float normalizedDistance)
        {
            const int samples=64;
            int count=knots!=null?knots.Length:0;
            bool dirty=cachedKnots==null || cachedKnots.Length!=count;
            if(!dirty)for(int i=0;i<count;i++)if(cachedKnots[i]!=Point(i)){dirty=true;break;}
            if(dirty)
            {
                cachedKnots=new Vector3[count];for(int i=0;i<count;i++)cachedKnots[i]=Point(i);
                arcLengths[0]=0;Vector3 before=SamplePosition(0);
                for(int i=1;i<=samples;i++){var next=SamplePosition(i/(float)samples);arcLengths[i]=arcLengths[i-1]+Vector3.Distance(before,next);before=next;}
            }
            float target=Mathf.Clamp01(normalizedDistance)*arcLengths[samples];
            for(int i=1;i<=samples;i++)if(arcLengths[i]>=target)return ((i-1)+Mathf.InverseLerp(arcLengths[i-1],arcLengths[i],target))/samples;
            return 1;
        }
        public CutsceneCameraState Sample(float t,bool constantSpeed=true)
        {
            float u=constantSpeed?DistanceToParameter(t):Mathf.Clamp01(t);Vector3 position=SamplePosition(u);
            Vector3 direction=lookAt!=null?lookAt.position+lookAtOffset-position:SamplePosition(Mathf.Min(1,u+0.01f))-SamplePosition(Mathf.Max(0,u-0.01f));
            Quaternion rotation=direction.sqrMagnitude>0.00001f?Quaternion.LookRotation(direction,Vector3.up):transform.rotation;
            if(lookAt!=null)rotation=Quaternion.LookRotation(direction+rotation*Vector3.right*framingOffset.x+rotation*Vector3.up*framingOffset.y,Vector3.up);
            return new CutsceneCameraState(position,rotation,fieldOfView);
        }
        void OnDrawGizmos()
        {
            Gizmos.color=new Color(0.3f,1,0.7f);Vector3 previous=SamplePosition(0);
            for(int i=1;i<=48;i++){var p=SamplePosition(i/48f);Gizmos.DrawLine(previous,p);previous=p;}
            if(knots!=null)foreach(var knot in knots)if(knot!=null)Gizmos.DrawWireSphere(knot.position,0.07f);
        }
    }
}
