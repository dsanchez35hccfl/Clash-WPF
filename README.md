# Clash-WPF

轻量的 Clash / Mihomo Windows 前端（WPF），用于管理内核、订阅、代理规则和 TUN 模式。

主要功能
- 启动 / 停止 / 重启 Clash / Mihomo 内核
- 管理订阅配置与 Profiles
- 显示流量与连接信息
- TUN 模式管理：安装、启用、禁用（保留 DLL 以便快速切换）

系统要求
- Windows 10 / 11
- .NET 10 SDK
- Visual Studio 2026 或等效 IDE（推荐使用 VS2026 Community）

构建与运行
1. 克隆仓库：
   ```powershell
   git clone <repo-url>
   cd Clash-WPF
   ```
2. 使用 Visual Studio 2026 打开解决方案并还原 NuGet 包。
3. 选择目标框架 `.NET 10`，编译并运行。

命令行 / 打包
- 程序支持使用提升权限运行的子进程执行 TUN 文件操作（用于自动安装/启用/禁用 wintun.dll）：
  - `--wintun install <dir>`  下载并写入 wintun.dll 到 `<dir>`（用于提升子进程模式）
  - `--wintun uninstall <dir>` 删除 wintun.dll
  - `--wintun enable <dir>`  将 `wintun.dll.disabled` 重命名回 `wintun.dll`
  - `--wintun disable <dir>` 将 `wintun.dll` 重命名为 `wintun.dll.disabled`

TUN 驱动（wintun.dll）行为说明
- "启用 TUN"（UI 开关）会：若目录已有 `wintun.dll.disabled`，则恢复为 `wintun.dll`；若不存在且需要写入，会尝试下载并写入（若缺少管理员权限，会弹出 UAC 提升以完成写入）。
- "禁用 TUN" 不会直接删除 `wintun.dll`，默认会将其重命名为 `wintun.dll.disabled`（保留文件），并停止内核以释放适配器。保留 DLL 可以在无需重新下载的情况下快速启用。
- 在可能的情况下，前端尽量只重启 core（内核进程），而不重启整个 WPF 客户端。

配置持久化
- 新增 `TunEnabled` 到应用配置（`clash-wpf.json`），用于持久化用户对 TUN 模式的启用/禁用偏好。

注意事项
- 如果程序检测到已安装 TUN 但当前非管理员运行，程序会建议或提示使用管理员权限以启用 TUN 模式。
- 若需要将应用以管理员身份整体重启，设置页提供“以管理员身份重启”按钮。

贡献
- 欢迎提交 issue 与 PR。请在 PR 说明中描述变更与测试步骤。

许可证
- 请根据仓库实际选择许可证并替换本节。

