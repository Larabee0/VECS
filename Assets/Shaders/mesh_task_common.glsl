#define TASK_LOCAL_SIZE_X  64
#define MESH_LOCAL_SIZE_X  32
#define MAX_MESH_VTX 64
#define MAX_MESH_TRIS 96
// set taskStartOffset to this when the MeshTaskCommand should be skipped in the task shader
#define TASK_COMMAND_SKIP  4294967295

struct Meshlet {
    uint vertex_offset;
	uint triangle_offset;
	uint vertex_count;
	uint triangle_count;
};

struct MeshTaskCommand {
    uint meshletOffset;
    uint meshletCount;
    uint taskStartOffset;
    uint triangleStartIndex;

    uint vertexStartIndex;
    uint absMatrixIndex;
    uint localMatrixIndex;
    uint16_t viewMask;
    uint8_t  layerFlags;
};

struct MeshTaskCullData{
	mat4 viewProj[6];
    uint16_t projectionBufferOffset;
    uint8_t maxLayers;
    uint8_t sphereCount;
    
	uint8_t includeMask;
	uint8_t excludeMask;
	uint8_t cullMode;    
    uint8_t projectionBufferSelect;

};
struct MeshletInfo {
    uint index;
    uint16_t viewMask;
    uint8_t verts;
    uint8_t tris;
};

struct MeshTaskPayload {
    uint taskIndex;
    int layerOffset;
    int layerCount;
    uint projectionCount;
    MeshletInfo perMeshlet[TASK_LOCAL_SIZE_X];
};


uint GetComputeShaderGroupCount(uint threadCount, uint localSize) {
	return max(1, (threadCount + localSize - 1) / localSize);
}

