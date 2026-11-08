

uvec4 GetPerMeshlet(){
	MeshletInfo info = payload.perMeshlet[gl_WorkGroupID.x];

    uint drawMeshlet = uint(InProjectionViewMask(payload.layerOffset + gl_WorkGroupID.y, info.viewMask));

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