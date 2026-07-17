#pragma once

#include "anity/ui/anity_ui_renderer.h"

#include <vector>

struct AnityUIDrawPacket {
  AnityUIBatchInfo info{};
  uint32_t firstIndex = 0;
  float clipXMin = 0.0f;
  float clipYMin = 0.0f;
  float clipXMax = 0.0f;
  float clipYMax = 0.0f;
  float softnessX = 0.0f;
  float softnessY = 0.0f;
};

/* C++-only render-thread snapshot. The Canvas mutex is held across batch build and copy. */
AnityResult AnityUICanvas_CopyFlattenedSnapshot(
    AnityUICanvas* canvas,
    std::vector<AnityUIPackedVertex>& vertices,
    std::vector<uint32_t>& indices,
    std::vector<AnityUIDrawPacket>& draws,
    AnityUICanvasStats& stats);
