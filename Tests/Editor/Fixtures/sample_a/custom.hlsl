#define LIL_CUSTOM_PROPERTIES \
    float _SampleA;

#define LIL_CUSTOM_TEXTURES \
    sampler2D _SampleAMask;

#define LIL_REQUIRE_APP_POSITION
#define LIL_REQUIRE_APP_NORMAL

#define LIL_CUSTOM_VERTEX_OS \
    positionOS.xyz *= 1.0;
