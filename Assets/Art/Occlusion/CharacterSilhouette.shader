Shader "Game/CharacterOcclusion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnitySprites.cginc"
            struct Output
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 maskPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };
            float4x4 _OcclusionVP;
            sampler2D _ObstacleMask;
            float _ActorOrder, _OutlinePixels, _Visibility, _OcclusionAvailable;
            fixed4 _FillColor, _OutlineColor;
            Output vert(appdata_t v)
            {
                Output o;
                v.vertex.xy *= _Flip.xy;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.maskPosition = mul(_OcclusionVP, mul(unity_ObjectToWorld, v.vertex));
                o.uv = v.texcoord;
                o.color = v.color * _Color * _RendererColor;
                return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                clip(_OcclusionAvailable - .5);
                float2 uv = i.maskPosition.xy / i.maskPosition.w * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
                #endif
                float2 obstacle = tex2D(_ObstacleMask, uv).rg;
                clip(obstacle.y - .5);
                clip(obstacle.x - _ActorOrder - .5);
                fixed alpha = SampleSpriteTexture(i.uv).a;
                clip(alpha - .1);
                float2 dx = ddx(i.uv) * _OutlinePixels;
                float2 dy = ddy(i.uv) * _OutlinePixels;
                fixed inner = min(min(SampleSpriteTexture(i.uv + dx).a, SampleSpriteTexture(i.uv - dx).a),
                                  min(SampleSpriteTexture(i.uv + dy).a, SampleSpriteTexture(i.uv - dy).a));
                fixed4 color = lerp(_OutlineColor, _FillColor, step(.1, inner));
                color.a *= alpha * i.color.a * _Visibility;
                return color;
            }
            ENDCG
        }
    }
}
