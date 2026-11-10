const uint16_t Layer_Project_None = uint16_t(0);
const uint16_t Layer_Project_0 = uint16_t(1);
const uint16_t Layer_Project_1 = uint16_t(2);
const uint16_t Layer_Project_2 = uint16_t(4);
const uint16_t Layer_Project_3 = uint16_t(8);
const uint16_t Layer_Project_4 = uint16_t(16);
const uint16_t Layer_Project_5 = uint16_t(32);
const uint16_t Layer_project_All = Layer_Project_0 | Layer_Project_1 | Layer_Project_2 | Layer_Project_3 | Layer_Project_4 | Layer_Project_5;
const uint16_t Layer_project_Dir_Light = Layer_Project_0 | Layer_Project_1 | Layer_Project_2 | Layer_Project_3 ;//| Layer_Project_4 | Layer_Project_5;

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

int GetLayerIndexFromBits(uint16_t mask){
    switch(uint(mask)){
        case 1:
            return 0;
        case 2:
            return 1;
        case 4:
            return 2;
        case 8:
            return 3;
        case 16:
            return 4;
        case 32:
            return 5;
        default:
            return 0;
    }
}

bool InProjectionViewMask(uint layerIndex, uint16_t viewMask) {

    uint16_t index = GetLayerProjectBitMask(layerIndex);

    return (index | viewMask) == viewMask;
}

uint16_t GetLayerBitsFromMask(uint index, uint16_t mask) {
		
    for(uint16_t i = Layer_Project_None, j = Layer_Project_None; i < 6; i++){
        j += InProjectionViewMask(i, mask) ? Layer_Project_0 : Layer_Project_None;
        if(j - 1 == index) return GetLayerProjectBitMask(i);
    }
    return Layer_Project_None;
}

uint GetMaskLayerCount(uint16_t mask){
    uint inLayers = 0;
    for(uint16_t i = Layer_Project_None; i < 6; i++){
        inLayers += InProjectionViewMask(i,mask) ? 1 : 0;
    }
    return inLayers;
}