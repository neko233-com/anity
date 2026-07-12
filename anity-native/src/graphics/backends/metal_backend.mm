#define ANITY_NATIVE_BUILD
#include "anity/graphics/anity_graphics.h"

#if defined(ANITY_HAS_METAL)
/* Future: MTLDevice / CAMetalLayer / EDR (HDR) on iOS/macOS */
#import <Metal/Metal.h>
#endif
