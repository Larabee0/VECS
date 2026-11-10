
#define EPSILON 1.0 / 512.0;


bvec4 cullingMode(uint8_t objectCullFlags, uint8_t cullDataIncludeMask, uint8_t cullDataExcludeMask, uint8_t cullMode){
    
    bool includeMask = (objectCullFlags | cullDataIncludeMask) == cullDataIncludeMask;
    bool excludeMask = (objectCullFlags & ~cullDataExcludeMask) == objectCullFlags;
    return bvec4(
    	includeMask && excludeMask, // x = visible in this pass

    	(uint8_t(1) | cullMode) == cullMode, // y = fustrum culling enable
    	(uint8_t(2) | cullMode) == cullMode, // z = distance culling enable
    	(uint8_t(4) | cullMode) == cullMode); // w = depth pryamid culling enable
}

float square(float x){
	return x * x;
}

bool AABB_Intersects_Sphere(vec4 bmin, vec4 bmax, vec4 sphere){
	float dmin = 0;
	for (int i = 0; i < 3; i++)
	{
		if(sphere[i] < bmin[i]) dmin += square(sphere[i] - bmin[i]); else
		if(sphere[i] > bmax[i]) dmin += square(sphere[i] - bmax[i]);
	}

	return dmin <= square(sphere.w);
}

bool AABB_Intersects_Sphere(ObjectBounds bounds, vec4 sphere){
	
	float cullOveride = bounds.bMin.w;
	if(cullOveride > 0.5 && cullOveride < 2) return true;

	return AABB_Intersects_Sphere(bounds.bMin, bounds.bMax, sphere);
}

bool IsVisible(vec4 bmin, vec4 bmax, vec4 g){
	return dot(g, bmin) < 0.0 &&
	    dot(g, vec4(bmax.x, bmin.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmax.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmax.x, bmax.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmin.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmax.x, bmin.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmax.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, bmax) < 0.0;
}

bool IsVisible(ObjectBounds bounds, bool distanceCulling, mat4 viewProj)
{
	viewProj = transpose(viewProj);
	vec4 bmin = bounds.bMin;
	float cullOveride = bmin.w;
	if(cullOveride > 0.5 && cullOveride < 2) return true;

	vec4 bmax = bounds.bMax;
	bmin.w = 1;
	bmax.w = 1;
	
	vec4 plane = viewProj[3] - viewProj[0]; // right
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	plane = viewProj[3] + viewProj[0]; // left
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	plane = viewProj[3] + viewProj[1]; // top
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	plane = viewProj[3] - viewProj[1]; // bottom
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	if(!distanceCulling) return true;

	plane = viewProj[3] - viewProj[2]; // near
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	plane = viewProj[3] + viewProj[2]; // far
	if(IsVisible(bmin,bmax,plane)){
		return false;
	}

	return true;
}

ObjectBounds AABBFromMinMax(vec3 min, vec3 max, float cullOveride){
	ObjectBounds val = {vec4(min,cullOveride),vec4(max,0)};
	return val;
}

vec3 Select(vec3 falseValue, vec3 trueValue, bvec3 test){
	return vec3(
		test.x ? trueValue.x : falseValue.x,
		test.y ? trueValue.y : falseValue.y,
		test.z ? trueValue.z : falseValue.z
	);
}

ObjectBounds TransformAABB(ObjectBounds aabb, mat3 transformation){
	vec3 t1 = transformation[0] * vec3(aabb.bMin.x);
	vec3 t2 = transformation[0] * vec3(aabb.bMax.x);
	bvec3 minMask = lessThan(t1, t2);
	ObjectBounds transformedAABB = AABBFromMinMax(Select(t2,t1,minMask),Select(t2,t1,not(minMask)),aabb.bMin.w);
	
	t1 = transformation[1] * vec3(aabb.bMin.y);
	t2 = transformation[1] * vec3(aabb.bMax.y);
	minMask = lessThan(t1, t2);
	transformedAABB.bMin.xyz += Select(t2, t1, minMask);
	transformedAABB.bMax.xyz += Select(t2, t1, not(minMask));

	t1 = transformation[2] * vec3(aabb.bMin.z);
	t2 = transformation[2] * vec3(aabb.bMax.z);
	minMask = lessThan(t1, t2);
	transformedAABB.bMin.xyz += Select(t2, t1, minMask);
	transformedAABB.bMax.xyz += Select(t2, t1, not(minMask));
	return transformedAABB;
}

ObjectBounds TransformAABB(ObjectBounds aabb, mat4 m){
	
	//aabb = TransformAABB(aabb, mat3((transformation)));
	
	//aabb.bMin.xyz += transformation[3].xyz;
	//aabb.bMax.xyz += transformation[3].xyz;
	//m = transpose(m);
	vec3 center = (aabb.bMax.xyz + aabb.bMin.xyz) * 0.5;
	vec3 extents = aabb.bMax.xyz - center;

	vec3 t_center = (m * vec4(center,1.0)).xyz;

	mat3 abs_Mat = mat3(abs(m[0].xyz),abs(m[1].xyz),abs(m[2].xyz));
	vec3 t_extent = abs_Mat * extents;

	aabb.bMin.xyz = t_center - t_extent;
	aabb.bMax.xyz = t_center + t_extent;

	return aabb;
}

float triangleArea(vec2 a, vec2 b, vec2 c) {
	return ((a.x - c.x) * (b.y - a.y) - (a.x - b.x) * (c.y - a.y)) * 0.5;
}

vec4 getSphere(mat4[6] matrices, uint index) {
	return matrices[index/4][index%4];
}