// Full-view fade for VR, used by SceneFader on a sphere around the camera.
//
// Draws the inside of the sphere (Cull Front) after everything else (last render
// queue, ZTest Always), so it covers the 3D scene, the video sphere and the UI —
// including UI drawn with Kyalio/UI-AlwaysOnTop in the Overlay queue.
//
// Alpha is written as coverage (One OneMinusSrcAlpha) so composition-layer UI
// behind a punched hole (OVROverlayCanvas underlay) fades with the rest.
Shader "Kyalio/ScreenFade"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay+1000"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Sphere"
        }

        Cull Front
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            Name "Fade"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _BaseColor;

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _BaseColor;
            }
        ENDCG
        }
    }
}
