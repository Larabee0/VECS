const uint16_t Layer_Project_None = uint16_t(0);
const uint16_t Layer_Project_0 = uint16_t(1);
const uint16_t Layer_Project_1 = uint16_t(2);
const uint16_t Layer_Project_2 = uint16_t(4);
const uint16_t Layer_Project_3 = uint16_t(8);
const uint16_t Layer_Project_4 = uint16_t(16);
const uint16_t Layer_Project_5 = uint16_t(32);
const uint16_t Layer_project_All = Layer_Project_0 | Layer_Project_1 | Layer_Project_2 | Layer_Project_3 | Layer_Project_4 | Layer_Project_5;

uint16_t GetLayerProjectBitMask(uint layerProjectIndex) {
    switch(layerProjectIndex){
        case 0:
            return Layer_Project_0;
        case 1:
            return Layer_Project_1;
        case 2:
            return Layer_Project_2;
        case 3:
            return Layer_Project_3;
        case 4:
            return Layer_Project_4;
        case 5:
            return Layer_Project_5;
        default:
            return Layer_Project_None;
    }
}

uint GetLayerCountFromProjectionSelect(uint projectionSelect){
    switch(projectionSelect){
        case 0:
            return 1;
        case 1:
            return 4;
        case 2:
            return 6;
        case 3:
            return 1;
        case 4:
            return 1;
        default:
            return 0;
    }
}

uint GetAmplificationCountFromProjectionSelect(uint projectionSelect){
    switch(projectionSelect){
        case 0:
            return 1;
        case 1:
            return 4;
        case 2:
            return 6;
        case 3:
            return 1;
        case 4:
            return 4;
        default:
            return 0;
    }
}

uvec4 GetPerMeshlet(){
	MeshletInfo info = payload.perMeshlet[gl_WorkGroupID.x];
    uint16_t index = uint16_t(gl_WorkGroupID.y);

    uint drawMeshlet = uint((index | info.viewMask) == info.viewMask);

    return uvec4(uint(info.index), uint(info.verts) * drawMeshlet, uint(info.tris) * drawMeshlet, drawMeshlet);
}

uvec2 GetOffsetCount(uint perInvoke, uint count){
    uint offset = perInvoke * gl_LocalInvocationID.x;
    if(offset >= count) {
        return uvec2(0, 0);
    }
    else if(perInvoke + offset > count) {
        perInvoke -= perInvoke + offset - count;
    }
    return uvec2(offset, perInvoke);
}