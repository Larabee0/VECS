#define TASK_COMMAND_SKIP  4294967295

struct ObjectBounds {
	vec4 bMin;
	vec4 bMax;
};


bvec4 cullingMode(uint8_t flags, uint8_t cullDataIncludeMask, uint8_t cullDataExcludeMask, uint8_t cullMode){
    
    bool includeMask = (flags | cullDataIncludeMask) == cullDataIncludeMask;
    bool excludeMask = (flags & ~cullDataExcludeMask) == flags;
    bvec4 cullModeInfo = bvec4(0);
    cullModeInfo.x = includeMask && excludeMask;

    cullModeInfo.y = (uint8_t(1) | cullMode) == cullMode;
    cullModeInfo.z = (uint8_t(2) | cullMode) == cullMode;
    cullModeInfo.w = (uint8_t(4) | cullMode) == cullMode;
    return cullModeInfo;
}


bool IsVisible(ObjectBounds bounds, int planeCount, vec4 frustums[6])
{
	vec4 bmin = bounds.bMin;
	float cullOveride = bmin.w;
	if(cullOveride > 0.5 && cullOveride < 2) return true;

	vec4 bmax = bounds.bMax;
	bmin.w = 1;
	bmax.w = 1;
	for (int i = 0; i < planeCount; i++)
	{
	    vec4 g = frustums[i];
	    bool visible =
		dot(g, bmin) < 0.0 &&
	    dot(g, vec4(bmax.x, bmin.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmax.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmax.x, bmax.y, bmin.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmin.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmax.x, bmin.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, vec4(bmin.x, bmax.y, bmax.z, 1.0)) < 0.0 &&
	    dot(g, bmax) < 0.0;

	    if (visible)
	    {
	        return false;
	    }
	}

	return true;
}