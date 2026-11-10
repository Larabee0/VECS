

uvec4 GetPerMeshlet() {
	MeshletInfo info = payload.perMeshlet[gl_WorkGroupID.x];

    uint drawMeshlet = uint(InProjectionViewMask(payload.layerOffset + gl_WorkGroupID.y, info.viewMask));

    return uvec4(uint(info.index), uint(info.verts) * drawMeshlet, uint(info.tris) * drawMeshlet, drawMeshlet);
}
