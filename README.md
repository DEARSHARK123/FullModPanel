# 完整版模组面板

Slay the Spire 2 早期体验模组。在主菜单 **设置 → 模组**（英文 Installed Mods）里给官方列表加分类、双名搜索和批量勾选。不换掉原版画面，不改战斗，也不做启动时的模组确认弹窗。

官方入口是设置界面上的模组按钮（`NOpenModdingScreenButton`），它打开 `NModdingScreen`。标题、左右蓝框、原版行（名字、Steam/文件夹图标、金色勾选）、右栏详情和红色返回都留着。本模组只在左栏蓝框里插入提示和搜索，并在滚动列表里按分类插标题。

## 安装

不依赖 BaseLib，也不需要 Godot。可装进游戏的文件只有 `artifacts/FullModPanel/` 里的 `FullModPanel.dll` 和 `FullModPanel.json`。分类逻辑已经编进这一个 dll，游戏不会加载旁边的 `FullModPanel.Core.dll`。步骤见下面的「本机出包 / 装测」。

## 怎么用

左栏自上而下：

1. 原版标题 Installed Mods。
2. 蓝框内、列表上方一行小字：**更改将在完全退出并重新启动游戏后生效。** 退回主菜单不够，进程要真正退出。
3. 同一行里：搜索框（占位符「搜索模组名称或工坊标题...」）和「全选当前」「反选当前」。范围只有**当前筛出来的行**。
4. 原版滚动列表。分类顺序固定：前置 / API → 角色与扩展 → 体验优化 · QoL → 玩法机制 · 调整 → 开发与调试 → 未分类。块标题是分类名、已勾选/可见数量、「全选本类」、「反选本类」。范围只有这一块里**正在显示**的行。
5. 每一行仍是原版：游戏内名、来源图标、金色勾选。只有创意工坊名和游戏内名不同时，名字下面多一行灰色副标题。

点一行，右栏仍由原版填作者、版本和简介。没有选中，或搜索把选中行滤掉时，右栏是空的。搜索没有命中时左栏显示「未找到匹配的模组」。空分类不画，全选和反选在 0 条时禁用。未分类只要这次结果里还有未分类模组就会出现，不会把模组悄悄丢掉。

## 双名

- 游戏内名 = 清单 `name`（没有 name 时用 `id`）
- 创意工坊名 = Steam 创意工坊标题；和游戏内名相同（忽略大小写和首尾空白）时不显示第二行
- 搜索两边都算

## 分类

只认这六个显示名：前置 / API、角色与扩展、体验优化 · QoL、玩法机制 · 调整、开发与调试、未分类。以后可以加，不能改名。

判断顺序：

1. **依赖图 / 像依赖库的清单。** 已知库 id（`BaseLib`、`RitsuLib`、`ModManagerSettings`、`ModConfig`）直接归入前置 / API。id、名字或简介像 library / framework / 依赖库，且没有别的类别关键词，也归入前置 / API。至少两个其他模组声明依赖它，且名字不像内容或平衡时，归入前置 / API。
2. **`affects_gameplay` 加名字/简介关键词。** 角色与扩展、玩法机制 · 调整需要 `affects_gameplay: true`。体验优化 · QoL 需要 `affects_gameplay: false`。开发与调试认「调试 / debug / console」等，弱的「工具」只在 `affects_gameplay: false` 时算。多个类别同时命中算冲突。
3. **创意工坊标题和标签。** 只有前两步都定不下来时才用。标签和标题互相矛盾则不定。
4. 其余、没把握、或信号冲突 → **未分类**。

`affects_gameplay` 在官方清单里默认是 `true`。只写了界面词、却留着默认值的模组会进未分类，而不是猜成体验优化 · QoL。

本面板不改 `ModList` 的顺序，加载顺序仍由游戏自己决定。

## 设置怎么存

和官方勾选同一条路径：

- 读：`ModManager.Mods`，是否启用看 `SaveManager.Instance.SettingsSave.ModSettings.IsModDisabled`
- 写：`ModSettings.ModList` 里对应的 `SettingsSaveMod`（`Id`、`Source`、`IsEnabled`）。没有这条就追加到末尾，不挪动已有顺序
- 落盘：`SaveManager.Instance.SaveSettings()`

单行金色勾选仍是原版 `NModMenuRow`：只改内存里的 `SettingsSaveMod`，等设置页关闭时由游戏保存。「全选当前 / 反选当前」和「全选本类 / 反选本类」会补上缺失的条目，并立刻调用 `SaveSettings()`。游戏内部的 `SaveSettings()` 失败时只打日志、不把错误抛出来，所以界面无法区分「已写入」和「游戏把写入吞掉」。加载与否仍以启动时的 `Mod.state` 为准，所以一定要完全重启。

这套类型是对照公开的 STS2 源码树核对过的（`NModdingScreen`、`NModMenuRow`、`SettingsSaveMod`、`ModSettings`、`SaveManager.SaveSettings`）。早期体验更新后签名变了的话，Harmony 找不到方法会在日志里说明，并留下官方列表。

## 本机出包 / 装测

需要已安装的 .NET 9 SDK。这个仓库不跑 Godot，清单 `has_pck` 是 `false`，所以没有 `.pck`。

在仓库根目录执行。`STS2_DATA_DIR` 指到**直接包含**下面四个文件的那一层，不是游戏根目录，也不是 `mods/`：

- `sts2.dll`
- `0Harmony.dll`
- `GodotSharp.dll`
- `Steamworks.NET.dll`

Steam：库中右键《Slay the Spire 2》→ 管理 → 浏览本地文件，再在该目录里找 `sts2.dll`。

Windows 常见：

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64
```

Linux 常见（以四个 dll 为准；有的安装目录名仍是 `data_sts2_windows_x86_64`）：

```text
~/.steam/steam/steamapps/common/Slay the Spire 2/data_sts2_linuxbsd_x86_64
~/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_linuxbsd_x86_64
```

macOS（Apple Silicon 上社区核对过的数据目录；本仓库不会自动探测 `.app` 里的路径，必须自己设置）：

```text
~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64
```

Windows PowerShell：

```powershell
$env:STS2_DATA_DIR = "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"
dotnet build .\src\FullModPanel\FullModPanel.csproj
```

macOS / Linux：

```bash
export STS2_DATA_DIR="$HOME/.steam/steam/steamapps/common/Slay the Spire 2/data_sts2_linuxbsd_x86_64"
dotnet build src/FullModPanel/FullModPanel.csproj
```

macOS 把 `export` 里的路径换成上面的 `data_sts2_macos_arm64`。成功后只使用这两个文件：

```text
artifacts/FullModPanel/FullModPanel.dll
artifacts/FullModPanel/FullModPanel.json
```

没设 `STS2_DATA_DIR`、或该目录里没有 `sts2.dll` 时，构建只会警告，并在 `src/FullModPanel/bin/` 留下不能装进游戏的占位程序集，**不会**写出 `artifacts/FullModPanel/`。不要拷贝 `bin/`、`.deps.json` 或 `.pdb`。

不编译游戏模组、只跑分类和列表面板测试：

```bash
dotnet test FullModPanel.sln
```

装进 `mods/FullModPanel/` 的只有这两个文件，不要额外的 json，也不要 `FullModPanel.Core.dll`。目录里如果还留着以前的 Core dll，删掉。

```text
Windows / Linux:
<游戏目录>/mods/FullModPanel/FullModPanel.dll
<游戏目录>/mods/FullModPanel/FullModPanel.json

macOS（在 .app 里，不在 Resources）:
SlayTheSpire2.app/Contents/MacOS/mods/FullModPanel/FullModPanel.dll
SlayTheSpire2.app/Contents/MacOS/mods/FullModPanel/FullModPanel.json
```

完全退出游戏后再启动。若出现官方的模组加载确认，同意后要再完全退出并重新启动一次。然后：主菜单 → 设置 → 模组（英文界面是 Modding）。左栏蓝框顶部有重启提示和搜索，列表按六个分类分开，行仍是原版图标和金色勾选。右栏仍是原版详情。

更改将在完全退出并重新启动游戏后生效。只退回主菜单不够。Steam 未启动或离线时，创意工坊标题不会出现，搜索只能命中游戏内名。

## 已知限制

- 创意工坊标题和标签来自 `SteamUGC` 查询（`SteamInitializer.Initialized` 为真时）。Steam 没起来、离线、或查询失败时没有第二行，分类也不会用到工坊标签。
- 早期体验的界面和存档 API 还会变。本模组按当前的 `NModdingScreen` 列表区域挂钩，不保证下一版节点名还在。
- 右栏仍是原版详情，不拿来扩列表。点一行显示作者和简介；搜索把该行滤掉时右栏清空。
- 不包含启动确认弹窗，也不卸载已经加载进进程的程序集。
