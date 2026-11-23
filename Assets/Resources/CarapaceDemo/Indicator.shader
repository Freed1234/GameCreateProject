Shader "CarapaceDemo/Indicator"
{
    Properties { _Progress("Charge", Range(0,1))=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Group,_Progress,_Length,_Radius,_HalfAngle,_OriginY;
            struct appdata { float4 vertex:POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float2 local:TEXCOORD0; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xy;return o;}
            float rectangle(float2 p,float halfWidth)
            {
                float2 q=abs(p-float2(_Length*.5,0))-float2(_Length*.5+_Radius,halfWidth);
                return length(max(q,0))+min(max(q.x,q.y),0);
            }
            float sector(float2 p,float halfAngle)
            {float a=radians(halfAngle);return max(length(p)-_Radius,abs(p.y)*cos(a)-p.x*sin(a));}
            fixed4 frag(v2f i):SV_Target
            {
                float outer,fill;
                float width=lerp(.015,_Radius,saturate(_Progress));
                if(_Group<.5){outer=rectangle(i.local,_Radius);fill=rectangle(i.local,width);}
                else if(_Group<1.5)
                {outer=sector(i.local,_HalfAngle);fill=sector(i.local,lerp(.25,_HalfAngle,saturate(_Progress)));}
                else
                {
                    outer=10000;fill=10000;float2 p=i.local-float2(0,_OriginY);
                    // Union mask: each screen pixel is blended only once, even at the shared origin.
                    [unroll] for(int j=0;j<8;j++)
                    {float a=j*UNITY_PI/4;float2 d=float2(cos(a),sin(a));float2 q=float2(dot(p,d),dot(p,float2(-d.y,d.x)));outer=min(outer,rectangle(q,_Radius));fill=min(fill,rectangle(q,width));}
                }
                if(abs(outer)<=.0225)return fixed4(1,1,1,1);
                if(outer<=0 && fill<=0)return fixed4(1,.06,.08,.5);
                discard;return 0;
            }
            ENDCG
        }
    }
}
