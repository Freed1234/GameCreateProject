Shader "Game/ObstacleMask"
{
    Properties { _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4x4 _OcclusionVP;
            sampler2D _MainTex;
            float _ObstacleOrder, _Opacity;
            Output vert(Input v)
            {
                Output o;
                o.vertex = mul(_OcclusionVP, mul(unity_ObjectToWorld, v.vertex));
                o.uv = v.uv;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a * _Opacity - .1);
                return float4(_ObstacleOrder, 1, 0, 1);
            }
            ENDCG
        }
    }
}
