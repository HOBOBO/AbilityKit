Shader "Hidden/AbilityKit/ShooterIndirectInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            StructuredBuffer<float4x4> _ShooterMatrices;
            fixed4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 positionWS = mul(_ShooterMatrices[input.instanceId], input.positionOS);
                output.positionCS = mul(UNITY_MATRIX_VP, positionWS);
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
