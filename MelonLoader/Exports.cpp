#include "Exports.h"
#include "MelonLoader.h"

IL2CPPDomain* melonloader_get_il2cpp_domain() { return IL2CPP::Domain; }
bool melonloader_is_il2cpp_game() { return MelonLoader::IsGameIL2CPP; }
const char* melonloader_getcommandline() { return GetCommandLine(); }
const char* melonloader_getgamepath() { return MelonLoader::GamePath; }
bool melonloader_is_debug_mode() { return MelonLoader::DebugMode; }