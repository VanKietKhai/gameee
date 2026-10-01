# Safety rules for this repository

These rules apply to anyone working in this git tree, including automation.

## Stay inside the repository

Do not read, write, copy, or delete files **outside this repository** as part of initializing or documenting the project.

In particular, do **not** access or modify operator-owned dedicated-server or backup trees, including (if present on a Windows host):

- `D:\ConanServer`
- `D:\ConanBackups`

Those directories, if they exist, belong to the person hosting the game. Repository setup must not create them, scan them, or change them.

## Software installation

Do not install system-wide software as part of repository initialization.

Building the solution later uses the .NET SDK the developer already has. SteamCMD is downloaded only when an operator explicitly uses the manager’s Install SteamCMD action, into a configured folder, not into this git tree.

## Administrator privileges

Do not run commands that require Administrator rights unless the operator has been asked and has agreed.

This manager is designed to run as a normal Windows user (`asInvoker`). It does not open Windows Firewall or router ports by itself.

## Secrets and licenses

- Never commit passwords, RCON secrets, or Steam credentials.
- Never add Conan client binaries or Workshop `.pak` files to git.
- Web Admin must stay off the public internet; Tailscale is the documented remote-access option.
