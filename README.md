# 更好的模组面板 (Better Mod Panel)

《杀戮尖塔 2》（Slay the Spire 2）抢先体验（EA）版模组。在原版 **设置 → 模组**（Installed Mods）界面基础上，增加自动分组、模组名/工坊名搜索和批量启闭功能，保持原版界面布局与操作习惯。

官方入口是设置界面上的模组按钮（`NOpenModdingScreenButton`），它打开 `NModdingScreen`。标题、左右蓝框、原版行（Steam/文件夹图标、金色勾选）、右栏详情和红色返回都留着。本模组只在左栏蓝框里插入搜索和批量按钮，并在滚动列表里按三个分组插标题。

## 安装

不依赖 BaseLib，也不需要 Godot。可装进游戏的文件是 `artifacts/FullModPanel/` 里的 `FullModPanel.dll`、`FullModPanel.json` 和 `mod_image.png`（游戏内详情图）。分类逻辑已经编进这一个 dll，游戏不会加载旁边的 `FullModPanel.Core.dll`。步骤见下面的「本机出包 / 装测」。

## 怎么用

左栏自上而下：

1. 原版标题 Installed Mods。
2. 搜索框（占位符「搜索模组名称或工坊标题...」）和「全选」「反选」。悬停提示「只作用于当前搜索结果」。反选是把当前结果里每一行翻转，不是全部关掉。
3. 原版滚动列表。分组顺序固定：依赖库 → 角色 / 玩法 / 内容 → 皮肤。块标题带已勾选/可见数量，以及同样的「全选」「反选」（悬停「只作用于本组」）。
4. 每一行保留原版图标和金色勾选。大字是创意工坊标题，小字是清单 `name`（两行均显示；没有工坊标题时，大字是清单名，小字是 `id`）。有扩展模组时，标题右侧带有一个折叠三角按钮和「（N 个扩展）」，默认收起，点击可展开。标题或小字过长时右端省略，悬停可查看完整文字。

点一行，右栏仍由原版填作者、版本和简介，标题仍是清单名。没有选中，或搜索把选中行滤掉时，右栏是空的。搜索没有命中时左栏显示「未找到匹配的模组」。空分组不画。

## 双名与拼音搜索

- 大字 = 创意工坊标题。没有工坊标题时用清单 `name`，再没有就用 `id`
- 小字 = 清单 `name`。没有工坊标题时小字是 `id`。两行都保留
- 搜索匹配列表上展示的两行文字（大字和小字）
- 拼音：两个字母以上的纯英文搜索，也能匹配中文标题。全拼（`haikesi` 命中「海克斯」）、首字母（`hks`）和混合（`haiks`）都可以。单个字母只做普通子串匹配，`m` 只命中可见文字里含 m 的行
- 搜索不区分大小写，忽略空格。每次输入后列表回到顶部，结果为空时显示「未找到匹配的模组」

## 语言

面板文字跟随游戏语言：简体中文（`zhs`）显示中文，其它语言显示英文。打开模组界面时读取一次；界面开着时切换语言，面板会立即刷新。清单里的模组名与创意工坊标题保持原样。

## 分组

固定分为三个类别：依赖库、角色 / 玩法 / 内容、皮肤。

判断顺序：

1. **依赖库。** 已知 id（`BaseLib`、`RitsuLib`、`STS2-RitsuLib`、`ModManagerSettings`、`ModConfig`）直接归入。名字或 id 像 library / framework，并且没有内容类工坊标签（Characters、Cards、Relics 等）时也归入。另一个已加载模组依赖它，且它带有 Tools & APIs 时也归入。依赖库内部按是否影响对局排序，影响对局的在前。
2. **角色 / 玩法 / 内容。** 不是依赖库，且清单省略了 `affects_gameplay` 或写成 `true`。
3. **皮肤。** 不是依赖库，且 `affects_gameplay` 为 `false`。不限于外观模组。

可通过 `user://FullModPanel/overrides.json` 手动指定模组的子类与父级扩展关系。

## 扩展

附着顺序：手动父级，然后依赖，然后标题前缀，然后 id 前缀。依赖库不能当父级。手动父级写了但找不到、或指向依赖库时，不再往下猜。同一分组里才缩进一行；跨组只在小字悬停里写「扩展自 X」。扩展的扩展收到最上层下面，只缩进一层。默认收起，全选和反选仍然包含收起的行。搜索只命中子项时，该行不缩进，悬停仍写「扩展自 X」。

本面板不改 `ModList` 的顺序，加载顺序仍由游戏自己决定。

## 设置怎么存

和官方勾选同一条路径：

- 读：`ModManager.Mods`，是否启用看 `SaveManager.Instance.SettingsSave.ModSettings.IsModDisabled`
- 写：`ModSettings.ModList` 里对应的 `SettingsSaveMod`（`Id`、`Source`、`IsEnabled`）。没有这条就追加到末尾，不挪动已有顺序
- 落盘：`SaveManager.Instance.SaveSettings()`

单行金色勾选仍是原版 `NModMenuRow`：只改内存里的 `SettingsSaveMod`，等设置页关闭时由游戏保存。「全选 / 反选」会补上缺失的条目，并立刻调用 `SaveSettings()`。批量开关通过游戏原生 `SettingsSave` 接口写入配置并立即保存；与原版一致，启闭模组需完全退出并重新启动游戏后生效。

若抢先体验（EA）版本更新改动了相关方法签名，Harmony 找不到目标方法时会在日志中记录说明，并退回使用官方原版列表。

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

macOS 把 `export` 里的路径换成上面的 `data_sts2_macos_arm64`。成功后使用这三个文件：

```text
artifacts/FullModPanel/FullModPanel.dll
artifacts/FullModPanel/FullModPanel.json
artifacts/FullModPanel/mod_image.png
```

没设 `STS2_DATA_DIR`、或该目录里没有 `sts2.dll` 时，构建只会警告，并在 `src/FullModPanel/bin/` 留下不能装进游戏的占位程序集，**不会**写出 `artifacts/FullModPanel/`。不要拷贝 `bin/`、`.deps.json` 或 `.pdb`。

不编译游戏模组、只跑分类和列表面板测试：

```bash
dotnet test FullModPanel.sln
```

装进 `mods/FullModPanel/` 的是这三个文件，不要额外的 json，也不要 `FullModPanel.Core.dll`。目录里如果还留着以前的 Core dll，删掉。

```text
Windows / Linux:
<游戏目录>/mods/FullModPanel/FullModPanel.dll
<游戏目录>/mods/FullModPanel/FullModPanel.json
<游戏目录>/mods/FullModPanel/mod_image.png

macOS（在 .app 里，不在 Resources）:
SlayTheSpire2.app/Contents/MacOS/mods/FullModPanel/FullModPanel.dll
SlayTheSpire2.app/Contents/MacOS/mods/FullModPanel/FullModPanel.json
SlayTheSpire2.app/Contents/MacOS/mods/FullModPanel/mod_image.png
```

完全退出游戏后再启动。若出现官方的模组加载确认，同意后要再完全退出并重新启动一次。然后：主菜单 → 设置 → 模组（英文界面是 Modding）。左栏蓝框顶部是搜索和全选 / 反选，列表按三个分组分开，行仍是原版图标和金色勾选。右栏仍是原版详情。

启用与否要完全退出并重新启动游戏后才会重新加载。只退回主菜单不够。Steam 未启动或离线时没有工坊标题，大字退回清单名，小字是 id，搜索仍可命中这两行上的文字。

## 已知限制

- 创意工坊标题和标签来自 `SteamUGC` 查询（`SteamInitializer.Initialized` 为真时）。Steam 没起来、离线、或查询失败时大字用清单名，依赖库也不会用到工坊标签。
- 游戏仍处于抢先体验（EA）阶段，后续官方更新若改动 `NModdingScreen` 列表节点或存档 API，可能需要跟进适配。
- 点一行在右栏显示原版作者、版本和简介；搜索把当前选中行筛掉时右栏清空。
- 与原版一致，勾选或取消模组后需重启游戏才会真正加载或卸载对应程序集。
