#version 460

#extension GL_ARB_shading_language_include : require
#include "smaa_defines.glsl"
#include "smaa_functions.glsl"

layout (location = 0) in vec2 vTexCoord0;
layout (location = 1) in vec4 vOffset[3];
layout (location = 0) out vec4 outFragColor;

#if defined(SMAA_EDGES_DEPTH) || SMAA_PREDICATION
layout (set = 1, binding = 0) uniform sampler2D uDepthTexture;
#endif

#if !defined(SMAA_EDGES_DEPTH) && SMAA_PREDICATION
layout (set = 1, binding = 1) uniform sampler2D uColourTexture;
#elif !defined(SMAA_EDGES_DEPTH) && !SMAA_PREDICATION
layout (set = 1, binding = 0) uniform sampler2D uColourTexture;
#endif

#ifdef SMAA_EDGES_LUMA
/**
 * Luma Edge Detection
 *
 * IMPORTANT NOTICE: luma edge detection requires gamma-corrected colors, and
 * thus 'colorTex' should be a non-sRGB texture.
 */
vec2 SMAALumaEdgeDetectionPS(vec2 texcoord,
                               vec4 offset[3],
                               sampler2D colorTex
                               #if SMAA_PREDICATION
                               , sampler2D predicationTex
                               #endif
                               ) {
    // Calculate the threshold:
    #if SMAA_PREDICATION
    vec2 threshold = SMAACalculatePredicatedThreshold(texcoord, offset, SMAATexturePass2D(predicationTex));
    #else
    vec2 threshold = vec2(SMAA_THRESHOLD, SMAA_THRESHOLD);
    #endif

    // Calculate lumas:
    vec3 weights = vec3(0.2126, 0.7152, 0.0722);
    float L = dot(texture(colorTex, texcoord).rgb, weights);

    float Lleft = dot(texture(colorTex, offset[0].xy).rgb, weights);
    float Ltop  = dot(texture(colorTex, offset[0].zw).rgb, weights);

    // We do the usual threshold:
    vec4 delta;
    delta.xy = abs(L - vec2(Lleft, Ltop));
    vec2 edges = step(threshold, delta.xy);

    // Then discard if there is no edge:
    if (dot(edges, vec2(1.0, 1.0)) == 0.0)
        discard;

    // Calculate right and bottom deltas:
    float Lright = dot(texture(colorTex, offset[1].xy).rgb, weights);
    float Lbottom  = dot(texture(colorTex, offset[1].zw).rgb, weights);
    delta.zw = abs(L - vec2(Lright, Lbottom));

    // Calculate the maximum delta in the direct neighborhood:
    vec2 maxDelta = max(delta.xy, delta.zw);

    // Calculate left-left and top-top deltas:
    float Lleftleft = dot(texture(colorTex, offset[2].xy).rgb, weights);
    float Ltoptop = dot(texture(colorTex, offset[2].zw).rgb, weights);
    delta.zw = abs(vec2(Lleft, Ltop) - vec2(Lleftleft, Ltoptop));

    // Calculate the final maximum delta:
    maxDelta = max(maxDelta.xy, delta.zw);
    float finalDelta = max(maxDelta.x, maxDelta.y);

    // Local contrast adaptation:
    edges.xy *= step(finalDelta, SMAA_LOCAL_CONTRAST_ADAPTATION_FACTOR * delta.xy);

    return edges;
}
#endif

#ifdef SMAA_EDGES_COLOR
/**
 * Color Edge Detection
 *
 * IMPORTANT NOTICE: color edge detection requires gamma-corrected colors, and
 * thus 'colorTex' should be a non-sRGB texture.
 */
vec2 SMAAColorEdgeDetectionPS(vec2 texcoord,
                                vec4 offset[3],
                                sampler2D colorTex
                                #if SMAA_PREDICATION
                                , sampler2D predicationTex
                                #endif
                                ) {
    // Calculate the threshold:
    #if SMAA_PREDICATION
    vec2 threshold = SMAACalculatePredicatedThreshold(texcoord, offset, predicationTex);
    #else
    vec2 threshold = vec2(SMAA_THRESHOLD, SMAA_THRESHOLD);
    #endif

    // Calculate color deltas:
    vec4 delta;
    vec3 C = texture(colorTex, texcoord).rgb;

    vec3 Cleft = texture(colorTex, offset[0].xy).rgb;
    vec3 t = abs(C - Cleft);
    delta.x = max(max(t.r, t.g), t.b);

    vec3 Ctop  = texture(colorTex, offset[0].zw).rgb;
    t = abs(C - Ctop);
    delta.y = max(max(t.r, t.g), t.b);

    // We do the usual threshold:
    vec2 edges = step(threshold, delta.xy);

    // Then discard if there is no edge:
    if (dot(edges, vec2(1.0, 1.0)) == 0.0)
        discard;

    // Calculate right and bottom deltas:
    vec3 Cright = texture(colorTex, offset[1].xy).rgb;
    t = abs(C - Cright);
    delta.z = max(max(t.r, t.g), t.b);

    vec3 Cbottom  = texture(colorTex, offset[1].zw).rgb;
    t = abs(C - Cbottom);
    delta.w = max(max(t.r, t.g), t.b);

    // Calculate the maximum delta in the direct neighborhood:
    vec2 maxDelta = max(delta.xy, delta.zw);

    // Calculate left-left and top-top deltas:
    vec3 Cleftleft  = texture(colorTex, offset[2].xy).rgb;
    t = abs(C - Cleftleft);
    delta.z = max(max(t.r, t.g), t.b);

    vec3 Ctoptop = texture(colorTex, offset[2].zw).rgb;
    t = abs(C - Ctoptop);
    delta.w = max(max(t.r, t.g), t.b);

    // Calculate the final maximum delta:
    maxDelta = max(maxDelta.xy, delta.zw);
    float finalDelta = max(maxDelta.x, maxDelta.y);

    // Local contrast adaptation:
    edges.xy *= step(finalDelta, SMAA_LOCAL_CONTRAST_ADAPTATION_FACTOR * delta.xy);

    return edges;
}
#endif

#ifdef SMAA_EDGES_DEPTH
/**
 * Depth Edge Detection
 */
vec2 SMAADepthEdgeDetectionPS(vec2 texcoord,
                                vec4 offset[3],
                                sampler2D depthTex) {
    vec3 neighbours = SMAAGatherNeighbours(texcoord, offset, depthTex);
    vec2 delta = abs(neighbours.xx - vec2(neighbours.y, neighbours.z));
    vec2 edges = step(SMAA_DEPTH_THRESHOLD, delta);

    if (dot(edges, vec2(1.0, 1.0)) == 0.0)
        discard;

    return edges;
}
#endif

void main()
{
    vec4 colour;

    #ifdef SMAA_EDGES_DEPTH
    colour.xy = SMAADepthEdgeDetectionPS(vTexCoord0, vOffset, uDepthTexture);
    #elif defined(SMAA_EDGES_LUMA)
    colour.xy= SMAALumaEdgeDetectionPS(vTexCoord0,vOffset,uColourTexture
    #if SMAA_PREDICATION
    ,uDepthTexture
    #endif
    );
    #elif defined(SMAA_EDGES_COLOR)
    color.xy = SMAAColorEdgeDetectionPS(vTexCoord0,vOffset,uColourTexture
    #if SMAA_PREDICATION
    ,uDepthTexture
    #endif
    );
    #endif
    outFragColor = colour;
}