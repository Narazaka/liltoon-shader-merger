#define LIL_CUSTOM_PROPERTIES \
    float _SampleB;

#define LIL_REQUIRE_APP_POSITION

#define LIL_CUSTOM_VERTEX_OS \
    positionOS.xyz += float3(0,0,0);

#define BEFORE_OUTPUT \
    fd.col *= 1.0;
