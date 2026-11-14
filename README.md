> [!CAUTION]
> This is an unofficial Bloxstrap fork. Download Bloxstrap Accounts only from [this repository's releases](https://github.com/Lanes1k/bloxstrap-accounts/releases), if available. The original Bloxstrap website and Winget package distribute the original application, not this fork.

> [!IMPORTANT]
> The original Bloxstrap project is no longer under active development. This fork is maintained separately; report fork-specific problems in this repository rather than to the original developers.

<p align="center">
    <picture>
        <source media="(prefers-color-scheme: dark)" srcset="Images/Bloxstrap-accounts-dark.png">
        <source media="(prefers-color-scheme: light)" srcset="Images/Bloxstrap-accounts-light.png">
        <img src="Images/Bloxstrap-accounts-light.png" alt="Bloxstrap accounts" width="520">
    </picture>
</p>

<div align="center">

[![License][shield-repo-license]][repo-license]

</div>

----

Bloxstrap is a third-party replacement for the standard Roblox bootstrapper, providing additional useful features and improvements.

**Bloxstrap Accounts** is an unofficial fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) with a Roblox account manager and experimental multi-instance support. Its WPF UI-based interface keeps the look and feel of Bloxstrap.

Running into a problem or need help with something? [Check out the original Bloxstrap Wiki](https://bloxstraplabs.com/wiki) for general features. If you can't find anything, or would like to suggest something for this fork, please [submit an issue](https://github.com/Lanes1k/bloxstrap-accounts/issues).

Bloxstrap is only supported for PCs running Windows.

## Frequently Asked Questions

**Q: Is this malware?**

**A:** This repository contains the source code for inspection. Source availability alone does not guarantee that a downloaded executable is safe or matches the source. Only run builds from a source you trust, especially when storing account sessions.

**Q: Can using this get me banned?**

**A:** No guarantee can be made for this fork's experimental multi-instance support. Statements about the original Bloxstrap do not automatically apply to this fork's changes. Follow Roblox's rules and use the feature at your own risk. [Read about the original Bloxstrap here.](https://bloxstraplabs.com/wiki/info/bloxstrap-and-bans)

## Features

- Hassle-free Discord Rich Presence to let your friends know what you're playing at a glance
- Simple support for modding of content files for customizability (death sound, mouse cursor, etc)
- See where your server is geographically located (courtesy of [ipinfo.io](https://ipinfo.io))
- Ability to configure graphics fidelity and UI experience
- A localized Roblox account manager with account selection and experimental multi-instance support

## Installing

Download a build from [Bloxstrap Accounts releases](https://github.com/Lanes1k/bloxstrap-accounts/releases), if available, and run it. Configure your preferences if needed, and install. If no builds have been published, build the project from source using the SDK specified in `global.json` and initialize the `wpfui` submodule.

The original Bloxstrap releases and `winget install bloxstrap` install the original application, **not Bloxstrap Accounts**.

You will also need the [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) for builds that do not bundle the runtime. .NET 6 is no longer supported by Microsoft; this project has not yet migrated to a supported version.

Windows SmartScreen may display a warning for an unfamiliar build. Verify its source and digital signature rather than automatically bypassing the warning. This fork's builds are not guaranteed to be signed.

Once installed, Bloxstrap is added to your Start Menu, where you can access the menu and reconfigure your preferences if needed.

## Using Accounts

1. Open the main menu and click **Account**.
2. Click **Add account** and choose a login method. Close running Roblox clients before using the Roblox login window.
3. Select a saved account for the next launch, or click **Launch** next to it.
4. Launch another account from the manager to open a separate client. Multi-instance compatibility depends on the current Roblox version.

The account manager follows Bloxstrap's selected interface language, including buttons, tooltips, login progress, and error messages. English is used as the fallback language.

If a session expires or is revoked, you may need to add the account again. If the sign-up screen appears instead of the expected account, the launch has not successfully authenticated that account.

## Account Security

> [!CAUTION]
> `.ROBLOSECURITY` is a session secret that grants access to an account. Never share it in issues, messages, screenshots, or logs. Only enter a cookie into a trusted local build whose code you are comfortable running.

Saved cookies are encrypted with Windows DPAPI for the current Windows user and stored in `Accounts.json` in Bloxstrap's data directory. The application decrypts them when needed; DPAPI does not fully protect against malicious software running with the same user's permissions.

Do not publish `Accounts.json` or other files containing session data. Removing an entry from the manager should not be treated as revoking the session on Roblox's servers. If you suspect a leak, terminate sessions through Roblox's security settings.

## Code

Bloxstrap uses the [WPF UI](https://github.com/lepoco/wpfui) library for the user interface design. This project uses the original Bloxstrap team's fork at [bloxstraplabs/wpfui](https://github.com/bloxstraplabs/wpfui).

Thanks to the original Bloxstrap contributors. Original copyright notices are preserved in the [MIT License](LICENSE); dependencies retain their own licenses.

## Code signing policy

The original Bloxstrap README credits [SignPath.io](https://signpath.io/) and the [SignPath Foundation](https://signpath.org/) for its code signing service and certificate. This does not mean that Bloxstrap Accounts builds are signed by those services.

[shield-repo-license]: https://img.shields.io/github/license/Lanes1k/bloxstrap-accounts

[repo-license]: LICENSE
