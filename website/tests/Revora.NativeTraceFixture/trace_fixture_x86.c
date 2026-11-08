#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>

#pragma pack(push, 1)
typedef struct trace_record {
    uint32_t magic;
    uint32_t stage;
    uint32_t sequence;
    uint32_t process_id;
    uint32_t thread_id;
    int64_t monotonic_timestamp;
} trace_record;
#pragma pack(pop)

static volatile LONG event_sequence = 0;

static int write_record(const char *path, uint32_t magic, uint32_t stage, uint32_t sequence) {
    LARGE_INTEGER counter;
    if (!QueryPerformanceCounter(&counter)) return 3;
    trace_record record = { magic, stage, sequence, GetCurrentProcessId(),
        GetCurrentThreadId(), counter.QuadPart };
    FILE *output = fopen(path, "wb");
    if (output == NULL) return 4;
    size_t written = fwrite(&record, sizeof(record), 1, output);
    int closed = fclose(output);
    return written == 1 && closed == 0 ? 0 : 5;
}

int main(int argc, char **argv) {
    if (argc != 3 || sizeof(void *) != 4 || sizeof(trace_record) != 28) return 2;
    int marker = write_record(argv[1], 0x484d5652u, 1u, 1u);
    if (marker != 0) return marker;
    LONG sequence = InterlockedIncrement(&event_sequence);
    return write_record(argv[2], 0x46345652u, 1u, (uint32_t)sequence);
}
