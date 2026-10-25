using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectCRT.Cutscenes
{
    public static class CutsceneTextUtility
    {
        static readonly HashSet<string> FormattingTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "b","i","u","s","strikethrough","color","alpha","mark","size","font","material","font-weight",
            "gradient","align","cspace","mspace","space","line-height","line-indent","indent","margin",
            "margin-left","margin-right","width","pos","voffset","rotate","uppercase","lowercase","smallcaps",
            "allcaps","sub","sup","link","nobr","style","br","page","sprite"
        };

        /// <summary>Extracts displayed characters, preserving ordinary angle brackets and noparse content.</summary>
        public static string PlainText(string text)
        {
            if(string.IsNullOrEmpty(text))return "";
            if(text.IndexOf('<')<0)return text;
            var result=new StringBuilder(text.Length);bool noParse=false;
            for(int i=0;i<text.Length;)
            {
                if(text[i]=='<' && TryReadTag(text,i,out int end,out string name,out bool closing))
                {
                    if(name.Equals("noparse",StringComparison.OrdinalIgnoreCase))
                    {
                        if(!noParse&&!closing){noParse=true;i=end+1;continue;}
                        if(noParse&&closing){noParse=false;i=end+1;continue;}
                    }
                    if(!noParse && (FormattingTags.Contains(name)||IsColorShorthand(name)))
                    {
                        if(!closing)
                        {
                            if(name.Equals("br",StringComparison.OrdinalIgnoreCase))result.Append('\n');
                            else if(name.Equals("page",StringComparison.OrdinalIgnoreCase))result.Append('\f');
                            else if(name.Equals("sprite",StringComparison.OrdinalIgnoreCase))result.Append('\uFFFC');
                        }
                        i=end+1;continue;
                    }
                }
                result.Append(text[i++]);
            }
            return result.ToString();
        }
        static bool TryReadTag(string text,int start,out int end,out string name,out bool closing)
        {
            end=-1;name="";closing=false;char quote='\0';
            for(int i=start+1;i<text.Length;i++)
            {
                char c=text[i];
                if(quote!='\0'){if(c==quote)quote='\0';continue;}
                if(c=='\''||c=='"'){quote=c;continue;}
                if(c=='<')return false;
                if(c=='>'){end=i;break;}
            }
            if(end<0)return false;
            int begin=start+1;
            if(begin<end&&text[begin]=='/'){closing=true;begin++;}
            if(begin>=end||char.IsWhiteSpace(text[begin]))return false;
            int finish=begin;
            while(finish<end&&!char.IsWhiteSpace(text[finish])&&text[finish]!='='&&text[finish]!='/')finish++;
            if(finish==begin)return false;
            name=text.Substring(begin,finish-begin);return true;
        }
        static bool IsColorShorthand(string value)
        {
            if(value.Length!=7&&value.Length!=9)return false;
            if(value[0]!='#')return false;
            for(int i=1;i<value.Length;i++)
            {
                char c=value[i];
                if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;
            }
            return true;
        }
        static int ScalarLength(string text,int index)=>char.IsHighSurrogate(text[index])&&index+1<text.Length&&char.IsLowSurrogate(text[index+1])?2:1;
        public static int CharacterCount(string text)
        {
            text=PlainText(text);int count=0;
            for(int i=0;i<text.Length;i+=ScalarLength(text,i))count++;
            return count;
        }
        /// <summary>Voice lookup is BMP-based. Supplementary glyphs and sprites are silent.</summary>
        public static char CharacterAt(string text,int index)
        {
            if(index<0)return '\0';text=PlainText(text);
            for(int i=0,count=0;i<text.Length;i+=ScalarLength(text,i),count++)
                if(count==index)return ScalarLength(text,i)==2||text[i]=='\uFFFC'?'\0':text[i];
            return '\0';
        }
        public static string Prefix(string text,int characterCount)
        {
            text=PlainText(text);if(characterCount<=0)return "";int end=0,count=0;
            while(end<text.Length&&count++<characterCount)end+=ScalarLength(text,end);
            return text.Substring(0,end);
        }
        static float Interval(float cps)=>1f/(float.IsNaN(cps)||cps<=0?1:Math.Max(1,cps));
        static float Pause(float seconds)=>float.IsNaN(seconds)?0:Math.Max(0,seconds);
        public static int RevealCount(string text, float elapsed, float cps, float punctuationPause)
        {
            text=PlainText(text);
            if(text.Length==0||elapsed<0)return 0;
            if(float.IsNaN(elapsed))elapsed=0;
            float time=0;int count=0;
            for(int i=0;i<text.Length;i+=ScalarLength(text,i))
            {
                if(time>elapsed)break;
                count++;time+=Interval(cps);
                if(IsPunctuation(text[i]))time+=Pause(punctuationPause);
            }
            return count;
        }
        public static float TextDuration(string text,float cps,float punctuationPause)
        {
            text=PlainText(text);float result=0;
            for(int i=0;i<text.Length;)
            {
                char c=text[i];i+=ScalarLength(text,i);
                // Time of the final glyph, matching RevealCount's first glyph at t=0.
                if(i>=text.Length)break;
                result+=Interval(cps);if(IsPunctuation(c))result+=Pause(punctuationPause);
            }
            return result;
        }
        public static bool IsPunctuation(char c)=>c=='.'||c==','||c=='!'||c=='?'||c=='…'||c=='。'||c=='、';
        public static string SpeakerName(CutsceneTurn turn,CutsceneStage stage)
        {
            if(turn==null||turn.nameMode==CutsceneNameMode.Hidden)return "";
            if(turn.nameMode==CutsceneNameMode.Alias)return turn.alias??"";
            if(turn.dialogueKind!=CutsceneDialogueKind.Spoken)return "";
            var actor=stage!=null?stage.FindActor(turn.speakerId):null;
            return actor!=null?actor.displayName:turn.speakerId??"";
        }
    }
}
