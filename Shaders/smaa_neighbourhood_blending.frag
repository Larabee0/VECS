#version 460
precision highp float;

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"

layout (location = 0) in vec2 vTexCoord0;
layout (location = 1) in vec4 vOffset;
layout (location = 0) out vec4 outFragColor;


layout(set = 0, binding = 0) uniform TexelSize{
	vec2 value;
} texelSize;

layout (set = 1, binding = 0) uniform sampler2D uColorTexture;
layout (set = 1, binding = 1) uniform sampler2D uBlendTexture;

#if SMAA_REPROJECTION                                  
layout (set = 1, binding = 2) uniform sampler2D uVelocityTexture;
#endif

//-----------------------------------------------------------------------------
// Neighborhood Blending Pixel Shader (Third Pass)

vec4 SMAANeighborhoodBlendingPS(vec2 texcoord,
                                  vec4 offset,
                                  sampler2D colorTex,
                                  sampler2D blendTex
                                  #if SMAA_REPROJECTION
                                  , sampler2D velocityTex
                                  #endif
                                  ) {
    // Fetch the blending weights for current pixel:
    vec4 a;
    a.x = texture(blendTex, offset.xy).a; // Right
    a.y = texture(blendTex, offset.zw).g; // Top
    a.wz = texture(blendTex, texcoord).xz; // Bottom / Left

    // Is there any blending weight with a value greater than 0.0?
    SMAA_BRANCH
    if (dot(a, vec4(1.0, 1.0, 1.0, 1.0)) < 1e-5) {
        vec4 color = textureLod(colorTex, texcoord, 0.0);

        #if SMAA_REPROJECTION
        vec2 velocity = SMAA_DECODE_VELOCITY(textureLod(velocityTex, texcoord, 0.0));

        // Pack velocity into the alpha channel:
        color.a = sqrt(5.0 * length(velocity));
        #endif

        return color;
    } else {
        bool h = max(a.x, a.z) > max(a.y, a.w); // max(horizontal) > max(vertical)

        // Calculate the blending offsets:
        vec4 blendingOffset = vec4(0.0, a.y, 0.0, a.w);
        vec2 blendingWeight = a.yw;
        SMAAMovc(bvec4(h, h, h, h), blendingOffset, vec4(a.x, 0.0, a.z, 0.0));
        SMAAMovc(bvec2(h, h), blendingWeight, a.xz);
        blendingWeight /= dot(blendingWeight, vec2(1.0, 1.0));

        // Calculate the texture coordinates:
        vec4 blendingCoord = fma(blendingOffset, vec4(SMAA_RT_METRICS.xy, -SMAA_RT_METRICS.xy), texcoord.xyxy);

        // We exploit bilinear filtering to mix current pixel with the chosen
        // neighbor:
        vec4 color = blendingWeight.x * textureLod(colorTex, blendingCoord.xy, 0.0);
        color += blendingWeight.y * textureLod(colorTex, blendingCoord.zw, 0.0);

        #if SMAA_REPROJECTION
        // Antialias velocity for proper reprojection in a later stage:
        vec2 velocity = blendingWeight.x * SMAA_DECODE_VELOCITY(textureLod(velocityTex, blendingCoord.xy, 0.0));
        velocity += blendingWeight.y * SMAA_DECODE_VELOCITY(textureLod(velocityTex, blendingCoord.zw, 0.0));

        // Pack velocity into the alpha channel:
        color.a = sqrt(5.0 * length(velocity));
        #endif

        return color;
    }
}

void main()
{
    outFragColor = SMAANeighborhoodBlendingPS(
        vTexCoord0,
        vOffset,
        uColorTexture,
        uBlendTexture
    #if SMAA_REPROJECTION
    ,uVelocityTexture
    #endif
    );
}