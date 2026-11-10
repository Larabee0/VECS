const uint16_t Layer_Project_None = uint16_t(0);
const uint16_t Layer_Project_0 = uint16_t(1);
//const uint16_t Layer_Project_1 = uint16_t(2);
//const uint16_t Layer_Project_2 = uint16_t(4);
//const uint16_t Layer_Project_3 = uint16_t(8);
//const uint16_t Layer_Project_4 = uint16_t(16);
//const uint16_t Layer_Project_5 = uint16_t(32);
//const uint16_t Layer_project_All = Layer_Project_0 | Layer_Project_1 | Layer_Project_2 | Layer_Project_3 | Layer_Project_4 | Layer_Project_5;
//const uint16_t Layer_project_Dir_Light = Layer_Project_0 | Layer_Project_1 | Layer_Project_2 | Layer_Project_3 ;//| Layer_Project_4 | Layer_Project_5;

uint16_t GetLayerProjectBitMask(uint layerProjectIndex) {
    if(layerProjectIndex > 15) return Layer_Project_None;
    return uint16_t(pow(2, layerProjectIndex));
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
    
    return int(log2(max(1,mask)));
}

bool InProjectionViewMask(uint layerIndex, uint16_t viewMask) {

    uint16_t index = GetLayerProjectBitMask(layerIndex);

    return (index | viewMask) == viewMask;
}

uint16_t GetLayerBitsFromMask(uint index, uint16_t mask) {
    if(mask == Layer_Project_None) return Layer_Project_None;
    for(uint16_t i = Layer_Project_None, j = Layer_Project_None; i < 15; i++){
        j += InProjectionViewMask(i, mask) ? Layer_Project_0 : Layer_Project_None;
        if(j - 1 == index) return GetLayerProjectBitMask(i);
    }
    return Layer_Project_None;
}

uint GetMaskLayerCount(uint16_t mask){
    uint inLayers = 0;
    if(mask == Layer_Project_None) return 0;
    for(uint16_t i = Layer_Project_None; i < 15; i++){
        inLayers += InProjectionViewMask(i,mask) ? 1 : 0;
    }
    return inLayers;
}