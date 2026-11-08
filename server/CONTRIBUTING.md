# Contributing

## Language

Use English for all project-owned content, including:

- Player-facing messages and dialogs
- Console and log output
- Source-code comments and documentation
- Configuration labels and descriptions
- New namespaces, types, members, variables, directories, and file names

Translate existing Spanish content when modifying related code.

Do not rename protocol constants, packet fields or layouts, database schema objects, serialized members, external API contracts, or other compatibility-sensitive identifiers unless the change includes a coordinated migration and compatibility verification.

## Compatibility

The legacy project targets .NET Framework 4.8. Changes must preserve wire-protocol behavior, persisted data compatibility, and externally consumed contracts.

## Verification

Build the affected projects and test translated player-facing flows. Confirm that format placeholders, command tokens, identifiers, numeric values, and text encoding remain unchanged where required.