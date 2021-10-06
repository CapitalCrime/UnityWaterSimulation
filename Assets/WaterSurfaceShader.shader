// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Point Cloud"
{
	Properties
	{
		_Colour("Colour", Color) = (1.0, 0.0, 0.0, 1.0)
		_MainTex("Particle Sprite", 2D) = "white" {}
		_SizeMul("Size Multiplier", Float) = 1
	}

		SubShader
		{
			Pass
			{
				Cull Back
				Lighting Off
				Zwrite Off

			Blend SrcAlpha OneMinusSrcAlpha
			//Blend One OneMinusSrcAlpha
			//Blend One One
			//Blend OneMinusDstColor One

			LOD 200

			Tags
			{
				"RenderType" = "Transparent"
				"Queue" = "Transparent"
				"IgnoreProjector" = "True"
			}

			CGPROGRAM

			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform sampler2D _MainTex;
			float _SizeMul;
			float4 _Colour;

			struct WaterParticle {
				float3 pos;
			};

			StructuredBuffer<WaterParticle> particles;
			StructuredBuffer<float3> quad;

			struct v2f
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
				float4 col : COLOR;
			};

			v2f vert(uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				v2f o;

				float3 q = quad[id];

				o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_V, float4(particles[inst].pos, 1.0f)) + float4(q, 0.0f) * _SizeMul);

				o.uv = q + 0.5;

				o.col = _Colour;

				return o;
			}

			fixed4 frag(v2f i) : COLOR
			{
				return i.col;
			}

			ENDCG
		}
		}
}