# Release Notes

## v1.0.0 - Initial Desktop WPF Release

Released: YYYY-MM-DD

### Highlights
- WPF 前端：页面化的 UI，包含 Overview、Proxies、Profiles、Connections、Rules、Logs、Settings 等视图。
- 内核管理：启动、停止、重启 Clash / Mihomo 内核（支持自动注入 external-controller / secret / mixed-port）。
- 替代了传统的安装 wintun 方式：支持自动下载并写入 `wintun.dll`，并在需要管理员权限时通过 UAC 提升子进程执行安装操作。
- TUN 模式按需启用/禁用：
  - 在设置页新增 `TunEnabled` 开关。`禁用` 操作不会删除 `wintun.dll`，而是将其重命名为 `wintun.dll.disabled` 以便快速恢复；`启用` 会恢复并重启内核。
  - 优先只重启 core（内核进程），避免不必要的前端重启。
- 样式优化：调整主题色以改善状态与订阅文字的可读性。

### Fixes
- 点击安装/卸载 TUN 时避免重复操作（已检测到文件则跳过下载/删除）。
- 在非管理员状态下提示提升或自动请求 UAC 用于单次安装操作。

### Breaking changes / Notes
- 引入了 `TunEnabled` 配置字段（`clash-wpf.json`），首次升级后会自动使用默认值 `true`，用户可以在设置页调整。

### How to upgrade
- 使用 Visual Studio 打开解决方案并构建；新版本会保持原有配置文件并添加 `TunEnabled` 字段。

### Known issues
- 某些受限文件夹（如 Program Files）中读写 `wintun.dll` 仍然需要管理员权限；在这些目录下启用/禁用可能需要 UAC 提示。

