﻿Shader "Hidden/TransDissolve" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
        _SourceTex ("Base (RGB)", 2D) = "white" {}
		_DissolveTex ("Base (RGB)", 2D) = "white" {}
		_EmissionTex ("Base (RGB)", 2D) = "white" {}
	}
	
	CGINCLUDE
		#include "UnityCG.cginc"

		struct v2f {
			 half4 pos : POSITION;
			 half2 uv : TEXCOORD0;
		 };
		
		sampler2D _MainTex;
        sampler2D _SourceTex;
		sampler2D _DissolveTex;
		sampler2D _EmissionTex;
		fixed2 _Params; //[x: dissolve power, y: emission thickness]
		fixed _t;
				
		half4 frag(v2f i) : COLOR {
			half4 original = tex2D(_MainTex, i.uv);
			half4 dmask = tex2D(_DissolveTex, i.uv);

			half4 cblend = tex2D(_SourceTex, i.uv);
			if (dmask.r < _Params.y + _Params.x)
				cblend = tex2D(_EmissionTex, i.uv);
			if (dmask.r <= _Params.y)
				cblend = original;

			return lerp(original, cblend, _t);
		}

		v2f vert(appdata_img v) {
			v2f o;
			o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
			o.uv = v.texcoord.xy;
			return o;
		}
	ENDCG

	Subshader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }      

			CGPROGRAM
			#pragma fragmentoption ARB_precision_hint_fastest 
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}
