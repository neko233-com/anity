#define ANITY_NATIVE_BUILD
#include "anity/jobs/anity_jobs.h"
#include <thread>
#include <vector>
#include <atomic>
#include <algorithm>

static int g_workers = 0;
static bool g_init = false;

extern "C" {

AnityResult ANITY_CALL AnityJobs_Initialize(int32_t workerCount) {
  if (workerCount <= 0)
    workerCount = static_cast<int32_t>(std::max(1u, std::thread::hardware_concurrency()));
  g_workers = workerCount;
  g_init = true;
  return ANITY_OK;
}

void ANITY_CALL AnityJobs_Shutdown(void) {
  g_init = false;
  g_workers = 0;
}

AnityResult ANITY_CALL AnityJobs_ScheduleParallel(
    AnityJobFunc func, void* userData, int32_t count, int32_t batchSize) {
  if (!func || count <= 0) return ANITY_ERR_INVALID_ARG;
  if (!g_init) AnityJobs_Initialize(0);
  if (batchSize <= 0) batchSize = 1;

  int workers = std::min(g_workers, count);
  if (workers <= 1) {
    for (int i = 0; i < count; ++i) func(userData, i);
    return ANITY_OK;
  }

  std::atomic<int> next{0};
  std::vector<std::thread> threads;
  threads.reserve(static_cast<size_t>(workers));
  for (int w = 0; w < workers; ++w) {
    threads.emplace_back([&]() {
      for (;;) {
        int start = next.fetch_add(batchSize);
        if (start >= count) break;
        int end = std::min(start + batchSize, count);
        for (int i = start; i < end; ++i) func(userData, i);
      }
    });
  }
  for (auto& t : threads) t.join();
  return ANITY_OK;
}

void ANITY_CALL AnityJobs_CompleteAll(void) {
  /* join already done in ScheduleParallel for this simple scheduler */
}

int32_t ANITY_CALL AnityJobs_GetWorkerCount(void) {
  return g_workers;
}

} // extern "C"
