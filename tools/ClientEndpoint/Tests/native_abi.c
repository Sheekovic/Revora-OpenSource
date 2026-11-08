/* Executes only synthetic code emitted by our tests, never Conquer.exe. */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#if !defined(_M_IX86)
#error This harness must compile as x86 to test the 6711 calling convention.
#endif
typedef unsigned char (__stdcall *Lookup)(int, char*, int, unsigned int*);
int main(int argc, char** argv) {
    unsigned char patch[87], tail[] = {0x81,0xc4,0xa8,0,0,0,0x5d,0xb0,0x42,0xc2,0x10,0};
    unsigned char* image;
    DWORD previous;
    FILE* file = NULL;
    int types[] = {2,0,1,3,0x102,-1};
    int capacities[] = {16,32,15,1,0,-1};
    size_t i;
    if (argc != 4 || fopen_s(&file, argv[1], "rb") != 0) return 1;
    if (fread(patch, 1, sizeof(patch), file) != sizeof(patch) || fgetc(file) != EOF) { fclose(file); return 2; }
    fclose(file);
    image = (unsigned char*)VirtualAlloc(NULL, 0x800000, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
    if (!image) return 3;
    memcpy(image + 0x11ceb6, patch, 7);
    memcpy(image + 0x11ceb6 + 7, tail, sizeof(tail));
    memcpy(image + 0x7662e4, patch + 7, 80);
    if (!VirtualProtect(image, 0x800000, PAGE_EXECUTE_READ, &previous)) return 4;
    FlushInstructionCache(GetCurrentProcess(), image, 0x800000);
    for (i = 0; i < sizeof(types)/sizeof(types[0]); ++i) {
        unsigned char buffer[32], expected[32];
        struct { unsigned int before, value, after; } port = {0x11223344, 0x55667788, 0x99aabbcc};
        unsigned char result;
        unsigned int stackBefore, stackAfter;
        Lookup lookup = (Lookup)(image + 0x11ceb6);
        memset(buffer, 0xcc, sizeof(buffer)); memcpy(expected, buffer, sizeof(buffer));
        if (types[i] == 2) { memset(expected + 8, 0, 16); memcpy(expected + 8, argv[2], strlen(argv[2])); }
        __asm mov stackBefore, esp
        result = lookup(types[i], (char*)buffer + 8, 16, &port.value);
        __asm mov stackAfter, esp
        if (stackBefore != stackAfter || memcmp(buffer, expected, sizeof(buffer)) != 0 ||
            port.before != 0x11223344 || port.after != 0x99aabbcc ||
            port.value != (types[i] == 2 ? (unsigned int)atoi(argv[3]) : 0x55667788) ||
            result != (types[i] == 2 ? 1 : 0x42)) return 5;
    }
    for (i = 0; i < sizeof(capacities)/sizeof(capacities[0]); ++i) {
        unsigned char buffer[32], expected[32], result;
        unsigned int port = 0x55667788;
        Lookup lookup = (Lookup)(image + 0x11ceb6);
        memset(buffer, 0xcc, sizeof(buffer)); memcpy(expected, buffer, sizeof(buffer));
        if (capacities[i] >= 16) { memset(expected + 8, 0, 16); memcpy(expected + 8, argv[2], strlen(argv[2])); }
        result = lookup(2, (char*)buffer + 8, capacities[i], &port);
        if (memcmp(buffer, expected, sizeof(buffer)) != 0 || result != (capacities[i] >= 16 ? 1 : 0x42) ||
            port != (capacities[i] >= 16 ? (unsigned int)atoi(argv[3]) : 0x55667788)) return 6;
        if (lookup(2, NULL, capacities[i], &port) != 0x42 || lookup(2, (char*)buffer + 8, capacities[i], NULL) != 0x42) return 7;
    }
    VirtualFree(image, 0, MEM_RELEASE);
    printf("PASS x86 stack, fallback, address/port bounds, null pointers and capacities: %s:%s\n", argv[2], argv[3]);
    return 0;
}
