
#define PI 3.1415926535897932384626433832795

// From http://filmicgames.com/archives/75
vec3 Uncharted2Tonemap(vec3 x) {
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	return ((x*(A*x+C*B)+D*E)/(x*(A*x+B)+D*F))-E/F;
}

// Normal Distribution function --------------------------------------
float D_GGX(float dotNH, float roughness) {
	float alpha = roughness * roughness;
	float alpha2 = alpha * alpha;
	float denom = dotNH * dotNH * (alpha2 - 1.0) + 1.0;
	return (alpha2)/(PI * denom*denom); 
}

// Geometric Shadowing function --------------------------------------
float G_SchlicksmithGGX(float dotNL, float dotNV, float roughness) {
	float r = (roughness + 1.0);
	float k = (r*r) / 8.0;
	float GL = dotNL / (dotNL * (1.0 - k) + k);
	float GV = dotNV / (dotNV * (1.0 - k) + k);
	return GL * GV;
}

// Fresnel function ----------------------------------------------------
vec3 F_Schlick(float cosTheta, vec3 F0) {
	return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 F_SchlickR(float cosTheta, vec3 F0, float roughness) {
	return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 prefilteredReflection(samplerCube prefilteredMap, vec3 R, float roughness) {
	const float MAX_REFLECTION_LOD = 9.0; // todo: param/const
	float lod = roughness * MAX_REFLECTION_LOD;
	float lodf = floor(lod);
	float lodc = ceil(lod);
	vec3 a = textureLod(prefilteredMap, R, lodf).rgb;
	vec3 b = textureLod(prefilteredMap, R, lodc).rgb;
	return mix(a, b, lod - lodf);
}

vec3 specularContribution(vec3 L, vec3 V, vec3 N, vec3 F0, vec3 albedo, vec2 metalRoughness, vec3 lightColor) {
	// Precalculate vectors and dot products	
	vec3 H = normalize (V + L);
	float dotNH = clamp(dot(N, H), 0.0, 1.0);
	float dotNV = clamp(dot(N, V), 0.0, 1.0);
	float dotNL = clamp(dot(N, L), 0.0, 1.0);


	vec3 color = vec3(0.0);

	if (dotNL > 0.0) {
		// D = Normal distribution (Distribution of the microfacets)
		float D = D_GGX(dotNH, metalRoughness.g); 
		// G = Geometric shadowing term (Microfacets shadowing)
		float G = G_SchlicksmithGGX(dotNL, dotNV, metalRoughness.g);
		// F = Fresnel factor (Reflectance depending on angle of incidence)
		vec3 F = F_Schlick(dotNV, F0);		
		vec3 spec = D * F * G / (4.0 * dotNL * dotNV + 0.001);		
		vec3 kD = (vec3(1.0) - F) * (1.0 - metalRoughness.r);			
		color += (kD * albedo / PI + spec) * dotNL;
	}

	return color *lightColor;
}

vec3 ambientComponent(
	samplerCube samplerIrradiance,
	samplerCube prefilteredMap,
	sampler2D samplerBRDFLUT,
	vec3 normal,
	vec3 toCamera,
	vec3 albedo,
	vec3 ambient,
	vec2 metalRoughness){
    
	vec3 F0 = vec3(0.04); 
	F0 = mix(F0, albedo, metalRoughness.r);

    float dotProd = dot(normal, toCamera);
	dotProd = isnan(dotProd) ? 0 : dotProd;
	vec2 brdf = texture(samplerBRDFLUT, vec2(max(dotProd, 0.0), metalRoughness.g)).rg;
	vec3 reflection = prefilteredReflection(prefilteredMap, reflect(-toCamera, normal), metalRoughness.g).rgb ;	
	vec3 irradiance = texture(samplerIrradiance, normal).rgb;
    
	// Diffuse based on irradiance
	vec3 diffuse = irradiance * albedo;
	vec3 F = F_SchlickR(max(dotProd, 0.0), F0, metalRoughness.g);
    
	// Specular reflectance
	vec3 specular = reflection * (F * brdf.x + brdf.y);
    
	// Ambient part
	vec3 kD = 1.0 - F;
	kD *= 1.0 -  metalRoughness.r;	  
	return ambient * (kD * (diffuse) + specular);
}

vec3 calculateNormal(sampler2D normalTex, vec2 uv, vec3 fragNormal, vec3 fragTangent) {

	vec3 texNormal = vec3(texture(normalTex, uv).rg, 0.0);
	texNormal.b = sqrt(1 - texNormal.x * texNormal.x - texNormal.y * texNormal.y);

	vec3 tangentNormal = texNormal * 2.0 - 1.0;

	vec3 N = normalize(fragNormal);
	vec3 T = normalize(fragTangent);
	vec3 B = normalize(cross(N, T));
	mat3 TBN = mat3(T, B, N);
	return normalize(TBN * tangentNormal);
	//return N;
}

void getMaskValues(vec4 maskValue, out vec2 metalRoughness, out vec3 ambient){
    
	metalRoughness = vec2(maskValue.r, 1 - maskValue.a);
    ambient = maskValue.ggg;
}