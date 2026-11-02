#define TASK_LOCAL_SIZE_X  64
#define MESH_LOCAL_SIZE_X  64


struct Meshlet {
    uint vertex_offset;
	uint triangle_offset;
	uint vertex_count;
	uint triangle_count;
};

// escaping my vec3 nightmares heavily
struct Bounds {
    vec4 centerRadius; // radius in w
    float cone_apex_x;
    float cone_apex_y;
    float cone_apex_z;
    vec4 cone_axis_cutoff; // cut off in w
    uint8_t cone_axis_x;
    uint8_t cone_axis_y;
    uint8_t cone_axis_z;
    uint8_t cone_cutoff;
};

struct MeshTaskCommand {
    uint meshletOffset;
    uint meshletCount;
    uint taskStartOffset;
    uint triangleStartIndex;
    uint vertexStartIndex;
    uint absMatrixIndex;
    uint localMatrixIndex;
	uint8_t layerFlags;
};

struct MeshletInfo {
    uint index;
    uint16_t verts;
    uint16_t tris;
};

struct MeshTaskPayload {
    uint TaskGroupID;
    uint taskIndex;
    uint meshletCount;
    uint meshletOffset;
    MeshletInfo perMeshlet[TASK_LOCAL_SIZE_X];
};


uint GetComputeShaderGroupCount(uint threadCount, uint localSize)
{
	return max(1, (threadCount + localSize - 1) / localSize);
}

uint GetPerInvoke(uint count){
	uint perInvoke = max(1, count / MESH_LOCAL_SIZE_X);
	if(count % MESH_LOCAL_SIZE_X != 0){
		perInvoke++;
	}
	return perInvoke;
}
