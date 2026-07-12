#pragma once
#include "../anity_core.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef void (ANITY_CALL *AnityJobFunc)(void* userData, int32_t index);

ANITY_API AnityResult ANITY_CALL AnityJobs_Initialize(int32_t workerCount /* 0 = hardware */);
ANITY_API void ANITY_CALL AnityJobs_Shutdown(void);
ANITY_API AnityResult ANITY_CALL AnityJobs_ScheduleParallel(
    AnityJobFunc func, void* userData, int32_t count, int32_t batchSize);
ANITY_API void ANITY_CALL AnityJobs_CompleteAll(void);
ANITY_API int32_t ANITY_CALL AnityJobs_GetWorkerCount(void);

#ifdef __cplusplus
}
#endif
