#include "ggwave/ggwave.h"
#include <cstring>

#ifdef _WIN32
#define ECHO_API extern "C" __declspec(dllexport)
#else
#define ECHO_API extern "C" __attribute__((visibility("default")))
#endif

// All buffer ownership stays with the caller. A handle is an upstream instance id.
ECHO_API int echo_max_payload() { return GGWave::kMaxLengthVariable; }
ECHO_API int echo_create(int inputRate, int outputRate, int mode) {
    if (inputRate < 8000 || outputRate < 8000) return -1;
    auto p = ggwave_getDefaultParameters();
    p.sampleRateInp = (float) inputRate;
    p.sampleRateOut = (float) outputRate;
    p.sampleFormatInp = GGWAVE_SAMPLE_FORMAT_F32;
    p.sampleFormatOut = GGWAVE_SAMPLE_FORMAT_F32;
    p.operatingMode = mode;
    return ggwave_init(p);
}
ECHO_API void echo_destroy(int handle) { if (handle >= 0) ggwave_free(handle); }
ECHO_API int echo_encode(int handle, const unsigned char *payload, int length, int protocol, int volume,
                          float *output, int capacitySamples) {
    if (handle < 0 || !payload || length < 1 || length > GGWave::kMaxLengthVariable ||
        protocol < 0 || protocol > GGWAVE_PROTOCOL_ULTRASOUND_FASTEST || volume < 1 || volume > 50) return -1;
    int required = ggwave_encode(handle, payload, length, (ggwave_ProtocolId)protocol, volume, nullptr, 2);
    if (!output) return required;
    if (required <= 0 || capacitySamples < required) return -2;
    int bytes = ggwave_encode(handle, payload, length, (ggwave_ProtocolId)protocol, volume, output, 0);
    return bytes < 0 ? bytes : bytes / (int)sizeof(float);
}
ECHO_API int echo_decode(int handle, const float *samples, int count, unsigned char *payload, int capacity) {
    if (handle < 0 || !samples || count <= 0 || !payload || capacity < GGWave::kMaxDataSize) return -2;
    return ggwave_ndecode(handle, samples, count * (int)sizeof(float), payload, capacity);
}
