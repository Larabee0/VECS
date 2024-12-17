float lerp(float a, float b, float t){
	return a + (b - a) * clamp(t, 0, 1);
}

vec4 lerp(vec4 a, vec4 b, float t){
        t = clamp(t, 0, 1);
        return vec4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
}

float inverseLerp(float a, float b, float value){

	return a != b ? clamp((value - a) / (b - a), 0, 1) : 0;
}

vec4 triplanar(vec3 vertPos, vec3 normal, float scale, sampler2D tex)
{
	// Calculate triplanar coordinates
    vec2 uvX = vertPos.zy * scale;
    vec2 uvY = vertPos.xz * scale;
    vec2 uvZ = vertPos.xy * scale;

    vec4 colX = texture(tex, uvX);
    vec4 colY = texture(tex, uvY);
    vec4 colZ = texture(tex, uvZ);
	// Square normal to make all values positive + increase blend sharpness
    vec3 blendWeight = normal * normal;
	// Divide blend weight by the sum of its components. This will make x + y + z = 1
    blendWeight /= dot(blendWeight, vec3(1));
    return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
}

vec4 triplanarUVOffset(vec3 vertPos, vec3 normal, vec2 uvOffset, float scale, sampler2D tex)
{

	// Calculate triplanar coordinates
    vec2 uvX = (vertPos.zy + uvOffset) * scale;
    vec2 uvY = (vertPos.xz + uvOffset) * scale;
    vec2 uvZ = (vertPos.xy + uvOffset) * scale;

    vec4 colX = texture(tex, uvX);
    vec4 colY = texture(tex, uvY);
    vec4 colZ = texture(tex, uvZ);
	// Square normal to make all values positive + increase blend sharpness
    vec3 blendWeight = normal * normal;
	// Divide blend weight by the sum of its components. This will make x + y + z = 1
    blendWeight /= dot(blendWeight, vec3(1));
    return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
}

vec4 triplanarArray(vec3 vertPos, vec3 normal, float scale, float index, sampler2DArray tex)
{

	// Calculate triplanar coordinates
    vec3 uvX = vec3(vertPos.zy * scale, index);
    vec3 uvY = vec3(vertPos.xz * scale, index);
    vec3 uvZ = vec3(vertPos.xy * scale, index);

    vec4 colX = texture(tex,uvX);
    vec4 colY = texture(tex,uvY);
    vec4 colZ = texture(tex,uvZ);
	// Square normal to make all values positive + increase blend sharpness
    vec3 blendWeight = normal * normal;
	// Divide blend weight by the sum of its components. This will make x + y + z = 1
    blendWeight /= dot(blendWeight, vec3(1));
    return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
}

float remap(float value, float min1, float max1, float min2, float max2){
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}
