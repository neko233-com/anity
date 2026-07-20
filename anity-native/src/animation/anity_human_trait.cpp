#include "anity/animation/anity_human_trait.h"

#include <array>

namespace {

struct MuscleEntry {
    const char* muscleName;
    const char* handleName;
    int32_t boneIndex;
    float defaultMin;
    float defaultMax;
    int32_t humanPartDof;
    int32_t dof;
};

struct BoneEntry {
    const char* boneName;
    int32_t parentBoneIndex;
    int32_t required;
    float defaultHierarchyMass;
    std::array<int32_t, 3> muscles;
};

// Exact Unity 2022.3 HumanTrait/MuscleHandle table, recovered through the
// public native-backed API. This is the canonical runtime table for Mecanim.
constexpr std::array<MuscleEntry, 95> kMuscles{{
    {"Spine Front-Back", "Spine Front-Back", 7, -40, 40, 0, 0},
    {"Spine Left-Right", "Spine Left-Right", 7, -40, 40, 0, 1},
    {"Spine Twist Left-Right", "Spine Twist Left-Right", 7, -40, 40, 0, 2},
    {"Chest Front-Back", "Chest Front-Back", 8, -40, 40, 0, 3},
    {"Chest Left-Right", "Chest Left-Right", 8, -40, 40, 0, 4},
    {"Chest Twist Left-Right", "Chest Twist Left-Right", 8, -40, 40, 0, 5},
    {"UpperChest Front-Back", "UpperChest Front-Back", 54, -20, 20, 0, 6},
    {"UpperChest Left-Right", "UpperChest Left-Right", 54, -20, 20, 0, 7},
    {"UpperChest Twist Left-Right", "UpperChest Twist Left-Right", 54, -20, 20, 0, 8},
    {"Neck Nod Down-Up", "Neck Nod Down-Up", 9, -40, 40, 1, 0},
    {"Neck Tilt Left-Right", "Neck Tilt Left-Right", 9, -40, 40, 1, 1},
    {"Neck Turn Left-Right", "Neck Turn Left-Right", 9, -40, 40, 1, 2},
    {"Head Nod Down-Up", "Head Nod Down-Up", 10, -40, 40, 1, 3},
    {"Head Tilt Left-Right", "Head Tilt Left-Right", 10, -40, 40, 1, 4},
    {"Head Turn Left-Right", "Head Turn Left-Right", 10, -40, 40, 1, 5},
    {"Left Eye Down-Up", "Left Eye Down-Up", 21, -10, 15, 1, 6},
    {"Left Eye In-Out", "Left Eye In-Out", 21, -20, 20, 1, 7},
    {"Right Eye Down-Up", "Right Eye Down-Up", 22, -10, 15, 1, 8},
    {"Right Eye In-Out", "Right Eye In-Out", 22, -20, 20, 1, 9},
    {"Jaw Close", "Jaw Close", 23, -10, 10, 1, 10},
    {"Jaw Left-Right", "Jaw Left-Right", 23, -10, 10, 1, 11},
    {"Left Upper Leg Front-Back", "Left Upper Leg Front-Back", 1, -90, 50, 2, 0},
    {"Left Upper Leg In-Out", "Left Upper Leg In-Out", 1, -60, 60, 2, 1},
    {"Left Upper Leg Twist In-Out", "Left Upper Leg Twist In-Out", 1, -60, 60, 2, 2},
    {"Left Lower Leg Stretch", "Left Lower Leg Stretch", 3, -80, 80, 2, 3},
    {"Left Lower Leg Twist In-Out", "Left Lower Leg Twist In-Out", 3, -90, 90, 2, 4},
    {"Left Foot Up-Down", "Left Foot Up-Down", 5, -50, 50, 2, 5},
    {"Left Foot Twist In-Out", "Left Foot Twist In-Out", 5, -30, 30, 2, 6},
    {"Left Toes Up-Down", "Left Toes Up-Down", 19, -50, 50, 2, 7},
    {"Right Upper Leg Front-Back", "Right Upper Leg Front-Back", 2, -90, 50, 3, 0},
    {"Right Upper Leg In-Out", "Right Upper Leg In-Out", 2, -60, 60, 3, 1},
    {"Right Upper Leg Twist In-Out", "Right Upper Leg Twist In-Out", 2, -60, 60, 3, 2},
    {"Right Lower Leg Stretch", "Right Lower Leg Stretch", 4, -80, 80, 3, 3},
    {"Right Lower Leg Twist In-Out", "Right Lower Leg Twist In-Out", 4, -90, 90, 3, 4},
    {"Right Foot Up-Down", "Right Foot Up-Down", 6, -50, 50, 3, 5},
    {"Right Foot Twist In-Out", "Right Foot Twist In-Out", 6, -30, 30, 3, 6},
    {"Right Toes Up-Down", "Right Toes Up-Down", 20, -50, 50, 3, 7},
    {"Left Shoulder Down-Up", "Left Shoulder Down-Up", 11, -15, 30, 4, 0},
    {"Left Shoulder Front-Back", "Left Shoulder Front-Back", 11, -15, 15, 4, 1},
    {"Left Arm Down-Up", "Left Arm Down-Up", 13, -60, 100, 4, 2},
    {"Left Arm Front-Back", "Left Arm Front-Back", 13, -100, 100, 4, 3},
    {"Left Arm Twist In-Out", "Left Arm Twist In-Out", 13, -90, 90, 4, 4},
    {"Left Forearm Stretch", "Left Forearm Stretch", 15, -80, 80, 4, 5},
    {"Left Forearm Twist In-Out", "Left Forearm Twist In-Out", 15, -90, 90, 4, 6},
    {"Left Hand Down-Up", "Left Hand Down-Up", 17, -80, 80, 4, 7},
    {"Left Hand In-Out", "Left Hand In-Out", 17, -40, 40, 4, 8},
    {"Right Shoulder Down-Up", "Right Shoulder Down-Up", 12, -15, 30, 5, 0},
    {"Right Shoulder Front-Back", "Right Shoulder Front-Back", 12, -15, 15, 5, 1},
    {"Right Arm Down-Up", "Right Arm Down-Up", 14, -60, 100, 5, 2},
    {"Right Arm Front-Back", "Right Arm Front-Back", 14, -100, 100, 5, 3},
    {"Right Arm Twist In-Out", "Right Arm Twist In-Out", 14, -90, 90, 5, 4},
    {"Right Forearm Stretch", "Right Forearm Stretch", 16, -80, 80, 5, 5},
    {"Right Forearm Twist In-Out", "Right Forearm Twist In-Out", 16, -90, 90, 5, 6},
    {"Right Hand Down-Up", "Right Hand Down-Up", 18, -80, 80, 5, 7},
    {"Right Hand In-Out", "Right Hand In-Out", 18, -40, 40, 5, 8},
    {"Left Thumb 1 Stretched", "LeftHand.Thumb.1 Stretched", 24, -20, 20, 6, 0},
    {"Left Thumb Spread", "LeftHand.Thumb.Spread", 24, -25, 25, 6, 1},
    {"Left Thumb 2 Stretched", "LeftHand.Thumb.2 Stretched", 25, -40, 35, 6, 2},
    {"Left Thumb 3 Stretched", "LeftHand.Thumb.3 Stretched", 26, -40, 35, 6, 3},
    {"Left Index 1 Stretched", "LeftHand.Index.1 Stretched", 27, -50, 50, 7, 0},
    {"Left Index Spread", "LeftHand.Index.Spread", 27, -20, 20, 7, 1},
    {"Left Index 2 Stretched", "LeftHand.Index.2 Stretched", 28, -45, 45, 7, 2},
    {"Left Index 3 Stretched", "LeftHand.Index.3 Stretched", 29, -45, 45, 7, 3},
    {"Left Middle 1 Stretched", "LeftHand.Middle.1 Stretched", 30, -50, 50, 8, 0},
    {"Left Middle Spread", "LeftHand.Middle.Spread", 30, -7.5, 7.5, 8, 1},
    {"Left Middle 2 Stretched", "LeftHand.Middle.2 Stretched", 31, -45, 45, 8, 2},
    {"Left Middle 3 Stretched", "LeftHand.Middle.3 Stretched", 32, -45, 45, 8, 3},
    {"Left Ring 1 Stretched", "LeftHand.Ring.1 Stretched", 33, -50, 50, 9, 0},
    {"Left Ring Spread", "LeftHand.Ring.Spread", 33, -7.5, 7.5, 9, 1},
    {"Left Ring 2 Stretched", "LeftHand.Ring.2 Stretched", 34, -45, 45, 9, 2},
    {"Left Ring 3 Stretched", "LeftHand.Ring.3 Stretched", 35, -45, 45, 9, 3},
    {"Left Little 1 Stretched", "LeftHand.Little.1 Stretched", 36, -50, 50, 10, 0},
    {"Left Little Spread", "LeftHand.Little.Spread", 36, -20, 20, 10, 1},
    {"Left Little 2 Stretched", "LeftHand.Little.2 Stretched", 37, -45, 45, 10, 2},
    {"Left Little 3 Stretched", "LeftHand.Little.3 Stretched", 38, -45, 45, 10, 3},
    {"Right Thumb 1 Stretched", "RightHand.Thumb.1 Stretched", 39, -20, 20, 11, 0},
    {"Right Thumb Spread", "RightHand.Thumb.Spread", 39, -25, 25, 11, 1},
    {"Right Thumb 2 Stretched", "RightHand.Thumb.2 Stretched", 40, -40, 35, 11, 2},
    {"Right Thumb 3 Stretched", "RightHand.Thumb.3 Stretched", 41, -40, 35, 11, 3},
    {"Right Index 1 Stretched", "RightHand.Index.1 Stretched", 42, -50, 50, 12, 0},
    {"Right Index Spread", "RightHand.Index.Spread", 42, -20, 20, 12, 1},
    {"Right Index 2 Stretched", "RightHand.Index.2 Stretched", 43, -45, 45, 12, 2},
    {"Right Index 3 Stretched", "RightHand.Index.3 Stretched", 44, -45, 45, 12, 3},
    {"Right Middle 1 Stretched", "RightHand.Middle.1 Stretched", 45, -50, 50, 13, 0},
    {"Right Middle Spread", "RightHand.Middle.Spread", 45, -7.5, 7.5, 13, 1},
    {"Right Middle 2 Stretched", "RightHand.Middle.2 Stretched", 46, -45, 45, 13, 2},
    {"Right Middle 3 Stretched", "RightHand.Middle.3 Stretched", 47, -45, 45, 13, 3},
    {"Right Ring 1 Stretched", "RightHand.Ring.1 Stretched", 48, -50, 50, 14, 0},
    {"Right Ring Spread", "RightHand.Ring.Spread", 48, -7.5, 7.5, 14, 1},
    {"Right Ring 2 Stretched", "RightHand.Ring.2 Stretched", 49, -45, 45, 14, 2},
    {"Right Ring 3 Stretched", "RightHand.Ring.3 Stretched", 50, -45, 45, 14, 3},
    {"Right Little 1 Stretched", "RightHand.Little.1 Stretched", 51, -50, 50, 15, 0},
    {"Right Little Spread", "RightHand.Little.Spread", 51, -20, 20, 15, 1},
    {"Right Little 2 Stretched", "RightHand.Little.2 Stretched", 52, -45, 45, 15, 2},
    {"Right Little 3 Stretched", "RightHand.Little.3 Stretched", 53, -45, 45, 15, 3},
}};

constexpr std::array<BoneEntry, 55> kBones{{
    {"Hips", -1, 1, 82.5, {-1, -1, -1}},
    {"LeftUpperLeg", 0, 1, 15, {23, 22, 21}},
    {"RightUpperLeg", 0, 1, 15, {31, 30, 29}},
    {"LeftLowerLeg", 1, 1, 5, {25, -1, 24}},
    {"RightLowerLeg", 2, 1, 5, {33, -1, 32}},
    {"LeftFoot", 3, 1, 1, {-1, 27, 26}},
    {"RightFoot", 4, 1, 1, {-1, 35, 34}},
    {"Spine", 0, 1, 40.5, {2, 1, 0}},
    {"Chest", 7, 0, 38, {5, 4, 3}},
    {"Neck", 54, 0, 5, {11, 10, 9}},
    {"Head", 9, 1, 4, {14, 13, 12}},
    {"LeftShoulder", 54, 0, 4.5, {-1, 38, 37}},
    {"RightShoulder", 54, 0, 4.5, {-1, 47, 46}},
    {"LeftUpperArm", 11, 1, 4, {41, 40, 39}},
    {"RightUpperArm", 12, 1, 4, {50, 49, 48}},
    {"LeftLowerArm", 13, 1, 2, {43, -1, 42}},
    {"RightLowerArm", 14, 1, 2, {52, -1, 51}},
    {"LeftHand", 15, 1, 0.5, {-1, 45, 44}},
    {"RightHand", 16, 1, 0.5, {-1, 54, 53}},
    {"LeftToes", 5, 0, 0.2, {-1, -1, 28}},
    {"RightToes", 6, 0, 0.2, {-1, -1, 36}},
    {"LeftEye", 10, 0, 0, {-1, 16, 15}},
    {"RightEye", 10, 0, 0, {-1, 18, 17}},
    {"Jaw", 10, 0, 0, {-1, 20, 19}},
    {"Left Thumb Proximal", 17, 0, 0, {-1, 56, 55}},
    {"Left Thumb Intermediate", 24, 0, 0, {-1, -1, 57}},
    {"Left Thumb Distal", 25, 0, 0, {-1, -1, 58}},
    {"Left Index Proximal", 17, 0, 0, {-1, 60, 59}},
    {"Left Index Intermediate", 27, 0, 0, {-1, -1, 61}},
    {"Left Index Distal", 28, 0, 0, {-1, -1, 62}},
    {"Left Middle Proximal", 17, 0, 0, {-1, 64, 63}},
    {"Left Middle Intermediate", 30, 0, 0, {-1, -1, 65}},
    {"Left Middle Distal", 31, 0, 0, {-1, -1, 66}},
    {"Left Ring Proximal", 17, 0, 0, {-1, 68, 67}},
    {"Left Ring Intermediate", 33, 0, 0, {-1, -1, 69}},
    {"Left Ring Distal", 34, 0, 0, {-1, -1, 70}},
    {"Left Little Proximal", 17, 0, 0, {-1, 72, 71}},
    {"Left Little Intermediate", 36, 0, 0, {-1, -1, 73}},
    {"Left Little Distal", 37, 0, 0, {-1, -1, 74}},
    {"Right Thumb Proximal", 18, 0, 0, {-1, 76, 75}},
    {"Right Thumb Intermediate", 39, 0, 0, {-1, -1, 77}},
    {"Right Thumb Distal", 40, 0, 0, {-1, -1, 78}},
    {"Right Index Proximal", 18, 0, 0, {-1, 80, 79}},
    {"Right Index Intermediate", 42, 0, 0, {-1, -1, 81}},
    {"Right Index Distal", 43, 0, 0, {-1, -1, 82}},
    {"Right Middle Proximal", 18, 0, 0, {-1, 84, 83}},
    {"Right Middle Intermediate", 45, 0, 0, {-1, -1, 85}},
    {"Right Middle Distal", 46, 0, 0, {-1, -1, 86}},
    {"Right Ring Proximal", 18, 0, 0, {-1, 88, 87}},
    {"Right Ring Intermediate", 48, 0, 0, {-1, -1, 89}},
    {"Right Ring Distal", 49, 0, 0, {-1, -1, 90}},
    {"Right Little Proximal", 18, 0, 0, {-1, 92, 91}},
    {"Right Little Intermediate", 51, 0, 0, {-1, -1, 93}},
    {"Right Little Distal", 52, 0, 0, {-1, -1, 94}},
    {"UpperChest", 8, 0, 26, {8, 7, 6}},
}};

} // namespace

extern "C" {

int32_t ANITY_CALL AnityHumanTrait_GetMuscleCount(void) {
    return static_cast<int32_t>(kMuscles.size());
}

int32_t ANITY_CALL AnityHumanTrait_GetBoneCount(void) {
    return static_cast<int32_t>(kBones.size());
}

int32_t ANITY_CALL AnityHumanTrait_GetRequiredBoneCount(void) {
    return 15;
}

AnityResult ANITY_CALL AnityHumanTrait_GetMuscleInfo(
    int32_t muscleIndex, AnityHumanTraitMuscleInfo* outInfo) {
    if (!outInfo || muscleIndex < 0 || muscleIndex >= static_cast<int32_t>(kMuscles.size()))
        return ANITY_ERR_INVALID_ARG;
    const MuscleEntry& source = kMuscles[static_cast<size_t>(muscleIndex)];
    *outInfo = {
        source.muscleName, source.handleName, source.boneIndex,
        source.defaultMin, source.defaultMax, source.humanPartDof, source.dof,
    };
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityHumanTrait_GetBoneInfo(
    int32_t boneIndex, AnityHumanTraitBoneInfo* outInfo) {
    if (!outInfo || boneIndex < 0 || boneIndex >= static_cast<int32_t>(kBones.size()))
        return ANITY_ERR_INVALID_ARG;
    const BoneEntry& source = kBones[static_cast<size_t>(boneIndex)];
    *outInfo = {
        source.boneName, source.parentBoneIndex, source.required,
        source.defaultHierarchyMass, source.muscles[0], source.muscles[1], source.muscles[2],
    };
    return ANITY_OK;
}

AnityResult ANITY_CALL AnityHumanTrait_FindMuscleHandle(
    int32_t humanPartDof, int32_t dof, int32_t* outMuscleIndex) {
    if (!outMuscleIndex) return ANITY_ERR_INVALID_ARG;
    *outMuscleIndex = -1;
    for (size_t index = 0; index < kMuscles.size(); ++index) {
        if (kMuscles[index].humanPartDof == humanPartDof && kMuscles[index].dof == dof) {
            *outMuscleIndex = static_cast<int32_t>(index);
            return ANITY_OK;
        }
    }
    return ANITY_ERR_INVALID_ARG;
}

} // extern "C"
