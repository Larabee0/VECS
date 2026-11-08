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

bool InProjectionViewMask(uint layerIndex, uint16_t viewMask) {

    uint16_t index = GetLayerProjectBitMask(layerIndex);

    return (index | viewMask) == viewMask;
}