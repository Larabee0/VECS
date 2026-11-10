
uvec2 GetOffsetCount(uint perInvoke, uint count) {
    uint offset = perInvoke * gl_LocalInvocationID.x;
    if(offset >= count) {
        return uvec2(0, 0);
    }
    else if(perInvoke + offset > count) {
        perInvoke -= perInvoke + offset - count;
    }
    return uvec2(offset, perInvoke);
}

bool isBehindCamera(vec3 pa, vec3 pb, vec3 pc) {
    return (pa.z < 0.0 && pb.z < 0.0 && pc.z < 0.0);
}

bool isInBounds(vec3 pa, vec3 pb, vec3 pc) {
    return ((pa.x > 1.0 && pb.x > 1.0 && pc.x > 1.0) || (pa.x < -1.0 && pb.x < -1.0 && pc.x < -1.0))
        && ((pa.y > 1.0 && pb.y > 1.0 && pc.y > 1.0) || (pa.y < -1.0 && pb.y < -1.0 && pc.y < -1.0));
}

shared uvec2 vertexClipNDC [MAX_MESH_VTX];
bool cullPrimative(uvec3 abc) {
    
    vec3 pa = vec3(unpackHalf2x16(vertexClipNDC[abc.x].x), uintBitsToFloat(vertexClipNDC[abc.x].y));
    vec3 pb = vec3(unpackHalf2x16(vertexClipNDC[abc.y].x), uintBitsToFloat(vertexClipNDC[abc.y].y));
    vec3 pc = vec3(unpackHalf2x16(vertexClipNDC[abc.z].x), uintBitsToFloat(vertexClipNDC[abc.z].y));

    return isBehindCamera(pa, pb, pc) || isInBounds(pa, pb, pc);
}