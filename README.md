# PreventLock（SendInput）

一个在系统托盘常驻的 Windows 控制台程序，用于在无人操作时模拟极微小的鼠标移动以防止电脑自动锁屏或熄屏。程序在控制台运行并在后台启动一个 STA UI 线程来支持托盘图标与全局热键。默认使用 `SendInput` 发送模拟输入，并在程序目录（exe 同目录）保存 `config.json`。

# 快速开始

1. 要求：Windows + .NET 6 或更高（目标框架应为 `net6.0-windows`，并在 csproj 中启用 `<UseWindowsForms>true</UseWindowsForms>`）。
2. 在项目根或源码目录运行：

   ```bash
   dotnet run
   ```

   或发布后直接运行 exe。运行时会在系统托盘显示图标，并在控制台打印启动信息与运行状态。
3. 控制台快捷键：按下 `P` 切换手动暂停，`E` 切换启用/禁用，`S` 显示当前状态，`Q` 退出。

# 配置说明（config.json）

程序会在应用目录下自动创建 `config.json`（以及 `config.comment.json`，该文件带有中文注释说明，供阅读参考，但程序只解析 `config.json`）。配置结构示例：

```json
{
  "Enabled": true,
  "MoveIntervalSeconds": 20,
  "IdleToStartSeconds": 30,
  "CheckIntervalMilliseconds": 1000,
  "StartInTray": true,
  "Hotkeys": {
    "TogglePauseModifiers": 3,
    "TogglePauseVKey": 80,
    "ToggleEnableModifiers": 3,
    "ToggleEnableVKey": 69,
    "ExitModifiers": 3,
    "ExitVKey": 81
  }
}
```

字段说明（简要）：

* `Enabled`：是否启用服务（true/false）。
* `MoveIntervalSeconds`：空闲时每隔多少秒发送一次模拟输入（整数，秒）。
* `IdleToStartSeconds`：无人操作超过多少秒后开始模拟（整数，秒）。
* `CheckIntervalMilliseconds`：检查空闲的轮询间隔（毫秒）。
* `StartInTray`：是否以托盘模式启动。
* `Hotkeys`：当前实现使用数值形式（位掩码 + vkey 值），详见下节。

> 程序启动时还会在同目录生成 `config.comment.json`，该文件包含带中文的详细注释与示例，方便你手动编辑。

# 热键配置（当前实现）

> 注意：当前代码使用数值形式表示热键（并未实现字符串解析）。如果你想用 `Ctrl+Alt+P` 这种可读字符串，请告诉我我可以添加该特性。

热键由两部分组成：

* `Modifiers`（位掩码）：`MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8`。例如 Ctrl+Alt = 2|1 = 3。
* `VKey`：主键的虚拟键码（取自 `System.Windows.Forms.Keys` 枚举的数值，比如 `Keys.P = 80`, `Keys.E = 69`, `Keys.Q = 81`）。

示例（与默认热键对应）：

* TogglePause：Ctrl+Alt+P → `TogglePauseModifiers = 3`、`TogglePauseVKey = 80`
* ToggleEnable：Ctrl+Alt+E → `ToggleEnableModifiers = 3`、`ToggleEnableVKey = 69`
* Exit：Ctrl+Alt+Q → `ExitModifiers = 3`、`ExitVKey = 81`

如果需要更直观的字符串格式（如 `"Ctrl+Alt+P"`），我可以把解析器加入并更新 `config.comment.json` 的示例。

# 图标与资源

程序尝试两种方式加载托盘图标：

1. 从嵌入资源加载（推荐）：将 `preventlock.ico` 放到 `Resources\preventlock.ico` 并在 `.csproj` 中声明为嵌入资源：

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\preventlock.ico" />
</ItemGroup>
```

程序会通过 `Assembly.GetManifestResourceNames()` 自动查找以 `preventlock.ico` 结尾的嵌入资源名并加载。

2. 回退到输出目录加载：若没有嵌入资源，程序会尝试从 `$(AppContext.BaseDirectory)\Resources\preventlock.ico` 加载。

注意：托盘图标必须是真正的 `.ico` 格式（多尺寸最好：16×16、32×32、48×48）。如果图标加载失败，程序会使用默认系统图标。

# 故障排查

* 无法注册热键：可能需要以管理员权限运行；控制台会输出警告信息。
* 托盘图标不显示或加载异常：

  1. 在 Rider 中确认 `.ico` 的 `Build Action` 为 `EmbeddedResource`（若 Rider GUI 不提供选项，请在 `.csproj` 中手动添加 `<EmbeddedResource Include="Resources\preventlock.ico" />`）。
  2. 程序会在启动时列出所有嵌入资源名（控制台日志），检查输出并确认资源名是否包含 `preventlock.ico`。
  3. 如果图标流不是 ICO 格式（即使后缀为 `.ico`），会抛出错误。请使用可靠工具生成多尺寸 `.ico` 文件再嵌入。
* 程序把自己的模拟输入识别为人为输入导致频繁开始/停止：这是已处理的问题 —— 程序会记录“最后一次模拟输入”的时间并忽略该时间点之前的 `GetLastInputInfo`，以区分真实的人为操作。

# 构建与发布

推荐发布为单文件可执行（可选）：

```bash
dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained false
```

如需包含 .NET 运行时，请把 `--self-contained true`。

请确保 `TargetFramework` 为 `net6.0-windows`（或更高）并在 csproj 中启用 `UseWindowsForms`，例如：

```xml
<PropertyGroup>
  <TargetFramework>net6.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

# 日志与调试

程序会在控制台输出启动信息、状态变化与每次模拟输入（含时间戳）。如果你希望隐藏控制台并把日志写入文件（例如 `app.log`），我可以帮你添加文件日志功能。

# 许可证

你可以自由修改并在内部使用这份代码。如果需要对外发布或商用，请根据公司政策选择合适的开源许可证并在仓库中添加 LICENSE 文件。

---

如需我把 README 直接推送到你的项目（添加为 `README.md` 文档）或把 `config` 的热键解析改为可读字符串/添加文件日志/自动把 icon 嵌入 csproj，我可以直接在画布里更新代码并给出一步步的操作说明。
