# Client patch versus loader decision

Decision: use the minimal launcher-started loader for the private canary.

A static derivative could add an import or bootstrap call, but TQSafe acceptance
of such a modified image is not proven. Proving it requires a separately
authorized client run. Shipping a complete derivative would also require a
separate distribution decision; a locally applied signed delta reduces but
does not eliminate that issue.

The existing x86 loader already gates the exact 18,594,824-byte DX9 image and
SHA-256 `4889B6DAB71068D687A9757F64FB1033E9CE1C560714DF3B62634229776A65CE`,
checks process path/PID, loads one reviewed DLL, invokes a versioned initializer,
and reports its exit result. It uses no `d3d9.dll` proxy. This is the smallest
evidence-backed option B. It is not a general injector and accepts no arbitrary
DLL or PID selection from launcher configuration.

Fixed RVAs and preimages are used; signature scanning is unnecessary. The
approved proprietary executable remains unchanged and is not redistributed.
