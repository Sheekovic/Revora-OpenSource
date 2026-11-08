# Client Distribution Operations

The repository root is `/srv/revora-client-repository`, mode `0700`, root-owned. Key directories and private keys are root-only. The authenticated Admin service receives traverse/read permission only for the fixed owner-test package; it cannot read signing keys, key backups, object storage, or production channel content.

Manual signing is a reviewed operator action: review canonical UTF-8 JSON and its SHA-256; run the built first-party signer locally as root with the reviewed payload, selected root-only key, and new output; verify with the embedded public key; record hashes; then publish by atomic rename.

Never expose private keys through an API, process output, environment dump, Git, nginx, AMP, or application configuration. Encrypted key backups and separate passphrase files remain root-only. Move one encrypted backup and its passphrase into independently protected storage before treating the key as disaster-recoverable.

Owner test downloads are fixed-name responses from authenticated Revora Admin with `application/octet-stream`, range support, no listing, and no upload route. Public website downloads remain Coming Soon.
